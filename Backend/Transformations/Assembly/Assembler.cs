using System;
using System.Collections.Generic;
using System.Linq;
using Backend.Utils;
using Model;
using Model.ThreeAddressCode.Instructions;
using Model.ThreeAddressCode.Values;
using Model.ThreeAddressCode.Visitor;
using Model.Types;
using ConvertInstruction = Model.ThreeAddressCode.Instructions.ConvertInstruction;
using CreateArrayInstruction = Model.ThreeAddressCode.Instructions.CreateArrayInstruction;
using CreateObjectInstruction = Model.ThreeAddressCode.Instructions.CreateObjectInstruction;
using IndirectMethodCallInstruction = Model.ThreeAddressCode.Instructions.IndirectMethodCallInstruction;
using LoadInstruction = Model.ThreeAddressCode.Instructions.LoadInstruction;
using LoadTokenInstruction = Model.ThreeAddressCode.Instructions.LoadTokenInstruction;
using MethodCallInstruction = Model.ThreeAddressCode.Instructions.MethodCallInstruction;
using SizeofInstruction = Model.ThreeAddressCode.Instructions.SizeofInstruction;
using StoreInstruction = Model.ThreeAddressCode.Instructions.StoreInstruction;
using SwitchInstruction = Model.ThreeAddressCode.Instructions.SwitchInstruction;
using Bytecode = Model.Bytecode;
using ConstrainedInstruction = Model.ThreeAddressCode.Instructions.ConstrainedInstruction;
using Instruction = Model.ThreeAddressCode.Instructions.Instruction;

namespace Backend.Transformations.Assembly
{
    public class Assembler
    {
        private readonly MethodDefinition method;

        public Assembler(MethodDefinition method)
        {
            if (!method.Body.Kind.Equals(MethodBodyKind.ThreeAddressCode))
            {
                throw new Exception("MethodBody must be in Three Address Code");
            }

            this.method = method;
        }

        public MethodBody Execute()
        {
            var body = new MethodBody(MethodBodyKind.Bytecode);

            body.MaxStack = method.Body.MaxStack; // FIXME 
            body.Parameters.AddRange(method.Body.Parameters);

            // this is updated later on. Needed to preserver variables that are declared but not used
            body.LocalVariables.AddRange(method.Body.LocalVariables);

            if (method.Body.Instructions.Count > 0)
            {
                var instructionTranslator = new InstructionTranslator(method.Body);
                instructionTranslator.Visit(method.Body);

                body.ExceptionInformation.AddRange(instructionTranslator.exceptionInformationBuilder.Build());
                body.Instructions.AddRange(instructionTranslator.translatedInstructions);
            }

            body.UpdateVariables();
            var newLocals = body.LocalVariables.OfType<LocalVariable>().OrderBy(local => local.Index).ToList();
            body.LocalVariables.Clear();
            body.LocalVariables.AddRange(newLocals);

            return body;
        }

        private class InstructionTranslator : InstructionVisitor
        {
            public readonly IList<Bytecode.Instruction> translatedInstructions = new List<Bytecode.Instruction>();
            public readonly ExceptionInformationBuilder exceptionInformationBuilder = new ExceptionInformationBuilder();

            private readonly MethodBody bodyToProcess;
            private uint offset;
            private readonly IDictionary<int, bool> ignoreInstruction = new Dictionary<int, bool>();

            public InstructionTranslator(MethodBody bodyToProcess)
            {
                this.bodyToProcess = bodyToProcess;
            }

            private void Add(Bytecode.Instruction instruction, uint nextInstructionOffset)
            {
                translatedInstructions.Add(instruction);
                offset += CilInstructionSizeCalculator.SizeOf(instruction, nextInstructionOffset);
            }

            public override bool ShouldVisit(Instruction instruction)
            {
                var shouldProcessInstruction = !ignoreInstruction.TryGetValue(bodyToProcess.Instructions.IndexOf(instruction), out _);
                return shouldProcessInstruction;
            }

            public override void Visit(PopInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(offset, Bytecode.BasicOperation.Pop);
                Add(basicInstruction, NextInstructionOffsetFor(instruction));
            }

            public override void Visit(BinaryInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(offset, OperationHelper.ToBasicOperation(instruction.Operation))
                {
                    OverflowCheck = instruction.OverflowCheck,
                    UnsignedOperands = instruction.UnsignedOperands
                };
                Add(basicInstruction, NextInstructionOffsetFor(instruction));
            }

            public override void Visit(UnaryInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(offset, OperationHelper.ToBasicOperation(instruction.Operation));
                Add(basicInstruction, NextInstructionOffsetFor(instruction));
            }

            // FIXME revisar los casos, hay algunos que no estoy seguro de que esten bien. se repiten caminos ademas (sobretodo por el reference)
            public override void Visit(LoadInstruction instruction)
            {
                Bytecode.Instruction bytecodeInstruction;
                if (instruction.Operand is TemporalVariable && instruction.Result is TemporalVariable)
                {
                    if (instruction.Operand.Equals(instruction.Result))
                    {
                        return;
                    }
                    else
                    {
                        bytecodeInstruction = new Bytecode.BasicInstruction(offset, Bytecode.BasicOperation.Dup);
                    }
                }
                else
                {
                    if (instruction.Result is LocalVariable loc)
                    {
                        bytecodeInstruction = new Bytecode.StoreInstruction(offset, loc);
                    }
                    else
                    {
                        switch (instruction.Operand)
                        {
                            case Constant constant:
                                bytecodeInstruction = new Bytecode.LoadInstruction(offset, Bytecode.LoadOperation.Value, constant);
                                break;
                            case LocalVariable localVariable:
                            {
                                bytecodeInstruction = new Bytecode.LoadInstruction(offset, Bytecode.LoadOperation.Content, localVariable);
                                break;
                            }
                            case Dereference dereference:
                            {
                                bytecodeInstruction = new Bytecode.LoadIndirectInstruction(offset, dereference.Type);
                                break;
                            }
                            case Reference reference:
                                switch (reference.Value)
                                {
                                    case ArrayElementAccess arrayElementAccess:
                                    {
                                        bytecodeInstruction = new Bytecode.LoadArrayElementInstruction(
                                            offset,
                                            Bytecode.LoadArrayElementOperation.Address,
                                            (ArrayType) arrayElementAccess.Array.Type) {Method = arrayElementAccess.Method};
                                        break;
                                    }

                                    case LocalVariable localVariable:
                                    {
                                        bytecodeInstruction = new Bytecode.LoadInstruction(offset, Bytecode.LoadOperation.Address, localVariable);
                                        break;
                                    }
                                    case InstanceFieldAccess instanceFieldAccess:
                                        bytecodeInstruction = new Bytecode.LoadFieldInstruction(
                                            offset,
                                            Bytecode.LoadFieldOperation.Address,
                                            instanceFieldAccess.Field);
                                        break;
                                    default:
                                        throw new Exception(); // TODO
                                }

                                break;
                            case ArrayLengthAccess _:
                                bytecodeInstruction = new Bytecode.BasicInstruction(offset, Bytecode.BasicOperation.LoadArrayLength);
                                break;
                            case VirtualMethodReference virtualMethodReference:
                                bytecodeInstruction = new Bytecode.LoadMethodAddressInstruction(
                                    offset,
                                    Bytecode.LoadMethodAddressOperation.Virtual,
                                    virtualMethodReference.Method);
                                break;
                            case StaticMethodReference staticMethodReference:
                                bytecodeInstruction = new Bytecode.LoadMethodAddressInstruction(
                                    offset,
                                    Bytecode.LoadMethodAddressOperation.Static,
                                    staticMethodReference.Method);
                                break;
                            case InstanceFieldAccess instanceFieldAccess:
                                bytecodeInstruction = new Bytecode.LoadFieldInstruction(
                                    offset,
                                    Bytecode.LoadFieldOperation.Content,
                                    instanceFieldAccess.Field);
                                break;
                            case StaticFieldAccess staticFieldAccess:
                                bytecodeInstruction = new Bytecode.LoadFieldInstruction(
                                    offset,
                                    Bytecode.LoadFieldOperation.Content,
                                    staticFieldAccess.Field);
                                break;
                            case ArrayElementAccess arrayElementAccess:
                            {
                                var type = (ArrayType) arrayElementAccess.Array.Type;
                                bytecodeInstruction = new Bytecode.LoadArrayElementInstruction(
                                    offset,
                                    Bytecode.LoadArrayElementOperation.Content,
                                    type) {Method = arrayElementAccess.Method};

                                break;
                            }
                            default: throw new Exception(); // TODO
                        }
                    }
                }

                Add(bytecodeInstruction, NextInstructionOffsetFor(instruction));
            }

            // FIXME revisar
            public override void Visit(StoreInstruction instruction)
            {
                Bytecode.Instruction storeInstruction;
                switch (instruction.Result)
                {
                    case ArrayElementAccess arrayElementAccess:
                    {
                        var type = (ArrayType) arrayElementAccess.Array.Type;
                        storeInstruction = new Bytecode.StoreArrayElementInstruction(offset, type) {Method = arrayElementAccess.Method};
                        break;
                    }
                    case Dereference dereference:
                    {
                        storeInstruction = new Bytecode.StoreIndirectInstruction(offset, dereference.Type);
                        break;
                    }
                    case InstanceFieldAccess instanceFieldAccess:
                        storeInstruction = new Bytecode.StoreFieldInstruction(offset, instanceFieldAccess.Field);
                        break;
                    case StaticFieldAccess staticFieldAccess:
                        storeInstruction = new Bytecode.StoreFieldInstruction(offset, staticFieldAccess.Field);
                        break;
                    default:
                        throw new Exception(); // TODO msg
                }

                Add(storeInstruction, NextInstructionOffsetFor(instruction));
            }

            public override void Visit(NopInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(offset, Bytecode.BasicOperation.Nop);
                Add(basicInstruction, NextInstructionOffsetFor(instruction));
            }

            public override void Visit(BreakpointInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(offset, Bytecode.BasicOperation.Breakpoint);
                Add(basicInstruction, NextInstructionOffsetFor(instruction));
            }

            public override void Visit(TryInstruction instruction)
            {
                // try with multiple handlers is modelled as multiple try instructions with the same label but different handlers.
                // if label matches with the current try, then increase the number of expected handlers. If not, begin a new try block 
                if (exceptionInformationBuilder.CurrentProtectedBlockStartsAt(offset))
                {
                    exceptionInformationBuilder.IncrementCurrentProtectedBlockExpectedHandlers();
                }
                else
                {
                    exceptionInformationBuilder.BeginProtectedBlockAt(offset);
                }
            }

            public override void Visit(FaultInstruction instruction)
            {
                exceptionInformationBuilder.AddHandlerToCurrentProtectedBlock(offset, ExceptionHandlerBlockKind.Fault);
            }

            public override void Visit(FinallyInstruction instruction)
            {
                exceptionInformationBuilder.AddHandlerToCurrentProtectedBlock(offset, ExceptionHandlerBlockKind.Finally);
            }

            public override void Visit(FilterInstruction instruction)
            {
                exceptionInformationBuilder.AddFilterHandlerToCurrentProtectedBlock(offset, instruction.kind, instruction.ExceptionType);
            }

            public override void Visit(CatchInstruction instruction)
            {
                exceptionInformationBuilder.AddHandlerToCurrentProtectedBlock(offset, ExceptionHandlerBlockKind.Catch, instruction.ExceptionType);
            }

            public override void Visit(ThrowInstruction instruction)
            {
                var basicInstruction = instruction.HasOperand
                    ? new Bytecode.BasicInstruction(offset, Bytecode.BasicOperation.Throw)
                    : new Bytecode.BasicInstruction(offset, Bytecode.BasicOperation.Rethrow);
                Add(basicInstruction, NextInstructionOffsetFor(instruction));
                exceptionInformationBuilder.EndCurrentProtectedBlockIfAppliesAt(offset);
            }

            public override void Visit(UnconditionalBranchInstruction instruction)
            {
                var target = Convert.ToUInt32(instruction.Target.Substring(2), 16);
                switch (instruction.Operation)
                {
                    case UnconditionalBranchOperation.Leave:
                    {
                        var branchInstruction = new Bytecode.BranchInstruction(offset, Bytecode.BranchOperation.Leave, target);
                        Add(branchInstruction, NextInstructionOffsetFor(instruction));
                        exceptionInformationBuilder.EndCurrentProtectedBlockIfAppliesAt(offset);
                        break;
                    }
                    case UnconditionalBranchOperation.Branch:
                    {
                        var branchInstruction = new Bytecode.BranchInstruction(offset, Bytecode.BranchOperation.Branch, target);
                        Add(branchInstruction, NextInstructionOffsetFor(instruction));
                        break;
                    }
                    case UnconditionalBranchOperation.EndFinally:
                    {
                        var branchInstruction = new Bytecode.BasicInstruction(offset, Bytecode.BasicOperation.EndFinally);
                        Add(branchInstruction, NextInstructionOffsetFor(instruction));
                        exceptionInformationBuilder.EndCurrentProtectedBlockAt(offset); // no more handlers after finally
                        break;
                    }
                    case UnconditionalBranchOperation.EndFilter:
                    {
                        // nothing is done with exceptionInformation since filter area is the gap between try end and handler start
                        var branchInstruction = new Bytecode.BasicInstruction(offset, Bytecode.BasicOperation.EndFilter);
                        Add(branchInstruction, NextInstructionOffsetFor(instruction));
                        break;
                    }
                    default: throw instruction.Operation.ToUnknownValueException();
                }
            }

            public override void Visit(ConvertInstruction instruction)
            {
                var convertInstruction = new Bytecode.ConvertInstruction(
                    offset,
                    OperationHelper.ToConvertOperation(instruction.Operation),
                    instruction.ConversionType)
                {
                    OverflowCheck = instruction.OverflowCheck,
                    UnsignedOperands = instruction.UnsignedOperands,
                };
                Add(convertInstruction, NextInstructionOffsetFor(instruction));
            }

            public override void Visit(ReturnInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(offset, Bytecode.BasicOperation.Return);
                Add(basicInstruction, NextInstructionOffsetFor(instruction));
            }

            public override void Visit(ConditionalBranchInstruction instruction)
            {
                var target = Convert.ToUInt32(instruction.Target.Substring(2), 16);
                var branchInstruction = new Bytecode.BranchInstruction(
                    offset,
                    OperationHelper.ToBranchOperation(instruction.Operation, instruction.RightOperand),
                    target);
                Add(branchInstruction, NextInstructionOffsetFor(instruction));
            }

            public override void Visit(SwitchInstruction instruction)
            {
                var targets = instruction.Targets.Select(target => Convert.ToUInt32(target.Substring(2), 16)).ToList();
                var switchInstruction = new Bytecode.SwitchInstruction(offset, targets);
                Add(switchInstruction, NextInstructionOffsetFor(instruction));
            }

            public override void Visit(SizeofInstruction instruction)
            {
                var sizeofInstruction = new Bytecode.SizeofInstruction(offset, instruction.MeasuredType);
                Add(sizeofInstruction, NextInstructionOffsetFor(instruction));
            }

            public override void Visit(LoadTokenInstruction instruction)
            {
                var loadTokenInstruction = new Bytecode.LoadTokenInstruction(offset, instruction.Token);
                Add(loadTokenInstruction, NextInstructionOffsetFor(instruction));
            }

            public override void Visit(MethodCallInstruction instruction)
            {
                var methodCallInstruction = new Bytecode.MethodCallInstruction(
                    offset,
                    OperationHelper.ToMethodCallOperation(instruction.Operation),
                    instruction.Method
                );
                Add(methodCallInstruction, NextInstructionOffsetFor(instruction));
            }

            public override void Visit(IndirectMethodCallInstruction instruction)
            {
                var indirectMethodCallInstruction = new Bytecode.IndirectMethodCallInstruction(offset, instruction.Function);
                Add(indirectMethodCallInstruction, NextInstructionOffsetFor(instruction));
            }

            public override void Visit(CreateObjectInstruction instruction)
            {
                var index = bodyToProcess.Instructions.IndexOf(instruction);
                var methodCallInstruction = (MethodCallInstruction) bodyToProcess.Instructions[index + 1];
                ignoreInstruction.Add(index + 1, true); // method call
                ignoreInstruction.Add(index + 2, true); // load

                var createObjectInstruction = new Bytecode.CreateObjectInstruction(offset, methodCallInstruction.Method);
                var nextInstructionOffset = NextInstructionOffsetFor((Instruction) bodyToProcess.Instructions[index + 3]);
                Add(createObjectInstruction, nextInstructionOffset);
            }

            public override void Visit(CopyMemoryInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(offset, Bytecode.BasicOperation.CopyBlock);
                Add(basicInstruction, NextInstructionOffsetFor(instruction));
            }

            public override void Visit(LocalAllocationInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(offset, Bytecode.BasicOperation.LocalAllocation);
                Add(basicInstruction, NextInstructionOffsetFor(instruction));
            }

            public override void Visit(InitializeMemoryInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(offset, Bytecode.BasicOperation.InitBlock);
                Add(basicInstruction, NextInstructionOffsetFor(instruction));
            }

            public override void Visit(InitializeObjectInstruction instruction)
            {
                var initObjInstruction = new Bytecode.InitObjInstruction(offset, instruction.TargetAddress.Type);
                Add(initObjInstruction, NextInstructionOffsetFor(instruction));
            }

            public override void Visit(CopyObjectInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(offset, Bytecode.BasicOperation.CopyObject);
                Add(basicInstruction, NextInstructionOffsetFor(instruction));
            }

            public override void Visit(CreateArrayInstruction instruction)
            {
                var arrayType = new ArrayType(instruction.ElementType, instruction.Rank);
                var createArrayInstruction = new Bytecode.CreateArrayInstruction(offset, arrayType)
                {
                    WithLowerBound = instruction.LowerBounds.Any(),
                    Constructor = instruction.Constructor
                };
                Add(createArrayInstruction, NextInstructionOffsetFor(instruction));
            }

            public override void Visit(PhiInstruction instruction) => throw new Exception();

            public override void Visit(ConstrainedInstruction instruction)
            {
                var constrainedInstruction = new Bytecode.ConstrainedInstruction(offset, instruction.ThisType);
                Add(constrainedInstruction, NextInstructionOffsetFor(instruction));
            }

            // FIXME mmm medio medio esto
            private uint NextInstructionOffsetFor(Instruction instruction)
            {
                var indexOf = bodyToProcess.Instructions.IndexOf(instruction) + 1;
                return indexOf == bodyToProcess.Instructions.Count
                    ? instruction.Offset
                    : bodyToProcess.Instructions[indexOf].Offset;
            }
        }
    }
}
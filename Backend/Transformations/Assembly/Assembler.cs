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

            return body;
        }

        private class InstructionTranslator : InstructionVisitor
        {
            public readonly IList<Bytecode.Instruction> translatedInstructions = new List<Bytecode.Instruction>();
            public readonly ExceptionInformationBuilder exceptionInformationBuilder = new ExceptionInformationBuilder();

            private readonly MethodBody bodyToProcess;
            private readonly IDictionary<int, bool> ignoreInstruction = new Dictionary<int, bool>();

            public InstructionTranslator(MethodBody bodyToProcess)
            {
                this.bodyToProcess = bodyToProcess;
            }

            public override bool ShouldVisit(Instruction instruction)
            {
                var shouldProcessInstruction = !ignoreInstruction.TryGetValue(bodyToProcess.Instructions.IndexOf(instruction), out _);
                return shouldProcessInstruction;
            }

            public override void Visit(PopInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.Pop);
                translatedInstructions.Add(basicInstruction);
            }

            public override void Visit(BinaryInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(instruction.Offset,
                    OperationHelper.ToBasicOperation(instruction.Operation))
                {
                    OverflowCheck = instruction.OverflowCheck,
                    UnsignedOperands = instruction.UnsignedOperands
                };
                translatedInstructions.Add(basicInstruction);
            }

            public override void Visit(UnaryInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(instruction.Offset,
                    OperationHelper.ToBasicOperation(instruction.Operation));
                translatedInstructions.Add(basicInstruction);
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
                        bytecodeInstruction = new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.Dup);
                    }
                }
                else
                {
                    if (instruction.Result is LocalVariable loc)
                    {
                        bytecodeInstruction = new Bytecode.StoreInstruction(instruction.Offset, loc);
                    }
                    else
                    {
                        switch (instruction.Operand)
                        {
                            case Constant constant:
                                bytecodeInstruction = new Bytecode.LoadInstruction(instruction.Offset,
                                    Bytecode.LoadOperation.Value, constant);
                                break;
                            case LocalVariable localVariable:
                            {
                                bytecodeInstruction = new Bytecode.LoadInstruction(instruction.Offset,
                                    Bytecode.LoadOperation.Content, localVariable);
                                break;
                            }
                            case Dereference dereference:
                            {
                                bytecodeInstruction = new Bytecode.LoadIndirectInstruction(instruction.Offset, dereference.Type);
                                break;
                            }
                            case Reference reference:
                                switch (reference.Value)
                                {
                                    case ArrayElementAccess arrayElementAccess:
                                    {
                                        bytecodeInstruction = new Bytecode.LoadArrayElementInstruction(
                                            instruction.Offset,
                                            Bytecode.LoadArrayElementOperation.Address,
                                            (ArrayType) arrayElementAccess.Array.Type) {Method = arrayElementAccess.Method};
                                        break;
                                    }

                                    case LocalVariable localVariable:
                                    {
                                        bytecodeInstruction = new Bytecode.LoadInstruction(instruction.Offset,
                                            Bytecode.LoadOperation.Address,
                                            localVariable);
                                        break;
                                    }
                                    case InstanceFieldAccess instanceFieldAccess:
                                        bytecodeInstruction = new Bytecode.LoadFieldInstruction(
                                            instruction.Offset,
                                            Bytecode.LoadFieldOperation.Address,
                                            instanceFieldAccess.Field);
                                        break;
                                    default:
                                        throw new Exception(); // TODO
                                }

                                break;
                            case ArrayLengthAccess _:
                                bytecodeInstruction = new Bytecode.BasicInstruction(instruction.Offset,
                                    Bytecode.BasicOperation.LoadArrayLength);
                                break;
                            case VirtualMethodReference virtualMethodReference:
                                bytecodeInstruction = new Bytecode.LoadMethodAddressInstruction(
                                    instruction.Offset,
                                    Bytecode.LoadMethodAddressOperation.Virtual,
                                    virtualMethodReference.Method);
                                break;
                            case StaticMethodReference staticMethodReference:
                                bytecodeInstruction = new Bytecode.LoadMethodAddressInstruction(
                                    instruction.Offset,
                                    Bytecode.LoadMethodAddressOperation.Static,
                                    staticMethodReference.Method);
                                break;
                            case InstanceFieldAccess instanceFieldAccess:
                                bytecodeInstruction = new Bytecode.LoadFieldInstruction(
                                    instruction.Offset,
                                    Bytecode.LoadFieldOperation.Content,
                                    instanceFieldAccess.Field);
                                break;
                            case StaticFieldAccess staticFieldAccess:
                                bytecodeInstruction = new Bytecode.LoadFieldInstruction(
                                    instruction.Offset,
                                    Bytecode.LoadFieldOperation.Content,
                                    staticFieldAccess.Field);
                                break;
                            case ArrayElementAccess arrayElementAccess:
                            {
                                var type = (ArrayType) arrayElementAccess.Array.Type;
                                bytecodeInstruction = new Bytecode.LoadArrayElementInstruction(
                                    instruction.Offset,
                                    Bytecode.LoadArrayElementOperation.Content,
                                    type) {Method = arrayElementAccess.Method};

                                break;
                            }
                            default: throw new Exception(); // TODO
                        }
                    }
                }

                translatedInstructions.Add(bytecodeInstruction);
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
                        storeInstruction = new Bytecode.StoreArrayElementInstruction(instruction.Offset, type)
                            {Method = arrayElementAccess.Method};
                        break;
                    }
                    case Dereference dereference:
                    {
                        storeInstruction = new Bytecode.StoreIndirectInstruction(instruction.Offset, dereference.Type);
                        break;
                    }
                    case InstanceFieldAccess instanceFieldAccess:
                        storeInstruction = new Bytecode.StoreFieldInstruction(instruction.Offset, instanceFieldAccess.Field);
                        break;
                    case StaticFieldAccess staticFieldAccess:
                        storeInstruction = new Bytecode.StoreFieldInstruction(instruction.Offset, staticFieldAccess.Field);
                        break;
                    default:
                        throw new Exception(); // TODO msg
                }

                translatedInstructions.Add(storeInstruction);
            }

            public override void Visit(NopInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.Nop);
                translatedInstructions.Add(basicInstruction);
            }

            public override void Visit(BreakpointInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.Breakpoint);
                translatedInstructions.Add(basicInstruction);
            }

            public override void Visit(TryInstruction instruction)
            {
                // try with multiple handlers is modelled as multiple try instructions with the same label but different handlers.
                // if label matches with the current try, then increase the number of expected handlers. If not, begin a new try block
                // FIxme add comment
                var offset = bodyToProcess.Instructions
                    .First(i => i.Offset > instruction.Offset && !(i is TryInstruction))
                    .Offset;
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
                // fixme estas deberian ser offset+1 tmb? replicar comment
                exceptionInformationBuilder.AddHandlerToCurrentProtectedBlock(instruction.Offset+1, ExceptionHandlerBlockKind.Fault);
            }

            public override void Visit(FinallyInstruction instruction)
            {
                // fixme estas deberian ser offset+1 tmb?
                exceptionInformationBuilder.AddHandlerToCurrentProtectedBlock(instruction.Offset+1, ExceptionHandlerBlockKind.Finally);
            }

            public override void Visit(FilterInstruction instruction)
            {
                // fixme estas deberian ser offset+1 tmb?
                exceptionInformationBuilder.AddFilterHandlerToCurrentProtectedBlock(instruction.Offset+1, instruction.kind, instruction.ExceptionType);
            }

            public override void Visit(CatchInstruction instruction)
            {
                // fixme estas deberian ser offset+1 tmb?  replicar comment
                exceptionInformationBuilder.AddHandlerToCurrentProtectedBlock(
                    instruction.Offset + 1,
                    ExceptionHandlerBlockKind.Catch,
                    instruction.ExceptionType);
            }

            public override void Visit(ThrowInstruction instruction)
            {
                var basicInstruction = instruction.HasOperand
                    ? new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.Throw)
                    : new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.Rethrow);
                translatedInstructions.Add(basicInstruction);
                exceptionInformationBuilder.EndCurrentProtectedBlockIfAppliesAt(instruction.Offset + 1); // block ends after instruction
            }

            public override void Visit(UnconditionalBranchInstruction instruction)
            {
                var target = Convert.ToUInt32(instruction.Target.Substring(2), 16);
                switch (instruction.Operation)
                {
                    case UnconditionalBranchOperation.Leave:
                    {
                        var branchInstruction =
                            new Bytecode.BranchInstruction(instruction.Offset, Bytecode.BranchOperation.Leave, target);
                        translatedInstructions.Add(branchInstruction);
                        exceptionInformationBuilder.EndCurrentProtectedBlockIfAppliesAt(instruction.Offset + 1); // block ends after instruction
                        break;
                    }
                    case UnconditionalBranchOperation.Branch:
                    {
                        var branchInstruction =
                            new Bytecode.BranchInstruction(instruction.Offset, Bytecode.BranchOperation.Branch, target);
                        translatedInstructions.Add(branchInstruction);
                        break;
                    }
                    case UnconditionalBranchOperation.EndFinally:
                    {
                        var branchInstruction =
                            new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.EndFinally);
                        translatedInstructions.Add(branchInstruction);
                        // no more handlers after finally
                        exceptionInformationBuilder.EndCurrentProtectedBlockAt(instruction.Offset + 1); // block ends after instruction 
                        break;
                    }
                    case UnconditionalBranchOperation.EndFilter:
                    {
                        // nothing is done with exceptionInformation since filter area is the gap between try end and handler start
                        var branchInstruction =
                            new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.EndFilter);
                        translatedInstructions.Add(branchInstruction);
                        break;
                    }
                    default: throw instruction.Operation.ToUnknownValueException();
                }
            }

            public override void Visit(ConvertInstruction instruction)
            {
                var convertInstruction = new Bytecode.ConvertInstruction(
                    instruction.Offset,
                    OperationHelper.ToConvertOperation(instruction.Operation),
                    instruction.ConversionType)
                {
                    OverflowCheck = instruction.OverflowCheck,
                    UnsignedOperands = instruction.UnsignedOperands,
                };
                translatedInstructions.Add(convertInstruction);
            }

            public override void Visit(ReturnInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.Return);
                translatedInstructions.Add(basicInstruction);
            }

            public override void Visit(ConditionalBranchInstruction instruction)
            {
                var target = Convert.ToUInt32(instruction.Target.Substring(2), 16);
                var branchInstruction = new Bytecode.BranchInstruction(
                    instruction.Offset,
                    OperationHelper.ToBranchOperation(instruction.Operation, instruction.RightOperand),
                    target);
                translatedInstructions.Add(branchInstruction);
            }

            public override void Visit(SwitchInstruction instruction)
            {
                var targets = instruction.Targets
                    .Select(target => Convert.ToUInt32(target.Substring(2), 16))
                    .ToList();
                var switchInstruction = new Bytecode.SwitchInstruction(instruction.Offset, targets);
                translatedInstructions.Add(switchInstruction);
            }

            public override void Visit(SizeofInstruction instruction)
            {
                var sizeofInstruction = new Bytecode.SizeofInstruction(instruction.Offset, instruction.MeasuredType);
                translatedInstructions.Add(sizeofInstruction);
            }

            public override void Visit(LoadTokenInstruction instruction)
            {
                var loadTokenInstruction = new Bytecode.LoadTokenInstruction(instruction.Offset, instruction.Token);
                translatedInstructions.Add(loadTokenInstruction);
            }

            public override void Visit(MethodCallInstruction instruction)
            {
                var methodCallInstruction = new Bytecode.MethodCallInstruction(
                    instruction.Offset,
                    OperationHelper.ToMethodCallOperation(instruction.Operation),
                    instruction.Method
                );
                translatedInstructions.Add(methodCallInstruction);
            }

            public override void Visit(IndirectMethodCallInstruction instruction)
            {
                var indirectMethodCallInstruction =
                    new Bytecode.IndirectMethodCallInstruction(instruction.Offset, instruction.Function);
                translatedInstructions.Add(indirectMethodCallInstruction);
            }

            public override void Visit(CreateObjectInstruction instruction)
            {
                var index = bodyToProcess.Instructions.IndexOf(instruction);
                var methodCallInstruction = (MethodCallInstruction) bodyToProcess.Instructions[index + 1];
                ignoreInstruction.Add(index + 1, true); // method call
                ignoreInstruction.Add(index + 2, true); // load

                var createObjectInstruction =
                    new Bytecode.CreateObjectInstruction(instruction.Offset, methodCallInstruction.Method);
                translatedInstructions.Add(createObjectInstruction);
            }

            public override void Visit(CopyMemoryInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.CopyBlock);
                translatedInstructions.Add(basicInstruction);
            }

            public override void Visit(LocalAllocationInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.LocalAllocation);
                translatedInstructions.Add(basicInstruction);
            }

            public override void Visit(InitializeMemoryInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.InitBlock);
                translatedInstructions.Add(basicInstruction);
            }

            public override void Visit(InitializeObjectInstruction instruction)
            {
                var initObjInstruction = new Bytecode.InitObjInstruction(instruction.Offset, instruction.TargetAddress.Type);
                translatedInstructions.Add(initObjInstruction);
            }

            public override void Visit(CopyObjectInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.CopyObject);
                translatedInstructions.Add(basicInstruction);
            }

            public override void Visit(CreateArrayInstruction instruction)
            {
                var arrayType = new ArrayType(instruction.ElementType, instruction.Rank);
                var createArrayInstruction = new Bytecode.CreateArrayInstruction(instruction.Offset, arrayType)
                {
                    WithLowerBound = instruction.LowerBounds.Any(),
                    Constructor = instruction.Constructor
                };
                translatedInstructions.Add(createArrayInstruction);
            }

            public override void Visit(PhiInstruction instruction) => throw new Exception();

            public override void Visit(ConstrainedInstruction instruction)
            {
                var constrainedInstruction = new Bytecode.ConstrainedInstruction(instruction.Offset, instruction.ThisType);
                translatedInstructions.Add(constrainedInstruction);
            }
        }
    }
}
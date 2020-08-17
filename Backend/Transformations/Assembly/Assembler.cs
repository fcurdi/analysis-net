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
    public class Assembler : IInstructionVisitor
    {
        private readonly MethodDefinition method;

        private readonly IList<Bytecode.Instruction> translatedInstructions;
        private readonly ExceptionInformationBuilder exceptionInformationBuilder;
        private readonly ISet<int> ignoredInstructions;
        private int Index { get; set; }

        public Assembler(MethodDefinition method)
        {
            if (!method.Body.Kind.Equals(MethodBodyKind.ThreeAddressCode))
            {
                throw new Exception("MethodBody must be in Three Address Code");
            }

            this.method = method;
            translatedInstructions = new List<Bytecode.Instruction>();
            exceptionInformationBuilder = new ExceptionInformationBuilder();
            ignoredInstructions = new HashSet<int>();
        }

        public MethodBody Execute()
        {
            var body = new MethodBody(MethodBodyKind.Bytecode);

            body.MaxStack = 20; // FIXME calcular (ver StackSize)
            body.Parameters.AddRange(method.Body.Parameters);
            // FIXME esto esta bien? porque las local variables se actualizan. La unica diferencia con eso era el this creo
            // no habira que en todo poner las locals variables mas el this si es que esta?

            for (Index = 0; Index < method.Body.Instructions.Count; Index++)
            {
                var instruction = (Instruction) method.Body.Instructions[Index];
                if (!ignoredInstructions.Contains(Index))
                {
                    instruction.Accept(this);
                }
            }

            body.ExceptionInformation.AddRange(exceptionInformationBuilder.Build());
            body.Instructions.AddRange(translatedInstructions);
            body.UpdateVariables();

            return body;
        }

        // abstract
        public void Visit(IInstructionContainer container)
        {
        }

        public void Visit(Instruction instruction)
        {
        }

        public void Visit(DefinitionInstruction instruction)
        {
        }

        public void Visit(BranchInstruction instruction)
        {
        }

        public void Visit(ExceptionalBranchInstruction instruction)
        {
        }
        //

        public void Visit(BinaryInstruction instruction)
        {
            if (instruction.Operation == BinaryOperation.Neq)
            {
                var basicInstruction = new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.Eq)
                {
                    Label = instruction.Label,
                    OverflowCheck = instruction.OverflowCheck,
                    UnsignedOperands = instruction.UnsignedOperands
                };
                translatedInstructions.Add(basicInstruction);

                basicInstruction = new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.Neg)
                {
                    Label = instruction.Label + "º", // ensure unique label
                    OverflowCheck = instruction.OverflowCheck,
                    UnsignedOperands = instruction.UnsignedOperands
                };
                translatedInstructions.Add(basicInstruction);
            }
            else
            {
                var basicInstruction = new Bytecode.BasicInstruction(instruction.Offset, OperationHelper.ToBasicOperation(instruction.Operation))
                {
                    Label = instruction.Label,
                    OverflowCheck = instruction.OverflowCheck,
                    UnsignedOperands = instruction.UnsignedOperands
                };
                translatedInstructions.Add(basicInstruction);
            }
        }

        public void Visit(UnaryInstruction instruction)
        {
            var basicInstruction = new Bytecode.BasicInstruction(instruction.Offset, OperationHelper.ToBasicOperation(instruction.Operation))
            {
                Label = instruction.Label
            };
            translatedInstructions.Add(basicInstruction);
        }

        public void Visit(LoadInstruction instruction)
        {
            Bytecode.Instruction bytecodeInstruction;
            if (instruction.Result is TemporalVariable)
            {
                switch (instruction.Operand)
                {
                    case TemporalVariable _:
                        bytecodeInstruction = new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.Dup);
                        break;
                    case Constant constant:
                        bytecodeInstruction = new Bytecode.LoadInstruction(instruction.Offset, Bytecode.LoadOperation.Value, constant);
                        break;
                    case LocalVariable localVariable:
                        bytecodeInstruction = new Bytecode.LoadInstruction(instruction.Offset, Bytecode.LoadOperation.Content, localVariable);
                        break;
                    case Dereference dereference:
                        bytecodeInstruction = new Bytecode.LoadIndirectInstruction(instruction.Offset, dereference.Type);
                        break;
                    case Reference reference:
                        switch (reference.Value)
                        {
                            case ArrayElementAccess arrayElementAccess:
                                bytecodeInstruction = new Bytecode.LoadArrayElementInstruction(
                                    instruction.Offset,
                                    Bytecode.LoadArrayElementOperation.Address,
                                    (ArrayType) arrayElementAccess.Array.Type) {Method = arrayElementAccess.Method};
                                break;

                            case LocalVariable localVariable:
                                bytecodeInstruction = new Bytecode.LoadInstruction(instruction.Offset, Bytecode.LoadOperation.Address, localVariable);
                                break;
                            case InstanceFieldAccess instanceFieldAccess:
                                bytecodeInstruction = new Bytecode.LoadFieldInstruction(
                                    instruction.Offset,
                                    Bytecode.LoadFieldOperation.Address,
                                    instanceFieldAccess.Field);
                                break;
                            default:
                                throw new CaseNotHandledException();
                        }

                        break;
                    case ArrayLengthAccess _:
                        bytecodeInstruction = new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.LoadArrayLength);
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
                        bytecodeInstruction = new Bytecode.LoadArrayElementInstruction(
                            instruction.Offset,
                            Bytecode.LoadArrayElementOperation.Content,
                            (ArrayType) arrayElementAccess.Array.Type) {Method = arrayElementAccess.Method};

                        break;
                    default:
                        throw new CaseNotHandledException();
                }
            }
            else
            {
                bytecodeInstruction = new Bytecode.StoreInstruction(instruction.Offset, (LocalVariable) instruction.Result);
            }

            bytecodeInstruction.Label = instruction.Label;
            translatedInstructions.Add(bytecodeInstruction);
        }

        public void Visit(StoreInstruction instruction)
        {
            Bytecode.Instruction storeInstruction;
            switch (instruction.Result)
            {
                case ArrayElementAccess arrayElementAccess:
                    storeInstruction = new Bytecode.StoreArrayElementInstruction(instruction.Offset, (ArrayType) arrayElementAccess.Array.Type)
                    {
                        Method = arrayElementAccess.Method
                    };
                    break;
                case Dereference dereference:
                    storeInstruction = new Bytecode.StoreIndirectInstruction(instruction.Offset, dereference.Type);
                    break;
                case InstanceFieldAccess instanceFieldAccess:
                    storeInstruction = new Bytecode.StoreFieldInstruction(instruction.Offset, instanceFieldAccess.Field);
                    break;
                case StaticFieldAccess staticFieldAccess:
                    storeInstruction = new Bytecode.StoreFieldInstruction(instruction.Offset, staticFieldAccess.Field);
                    break;
                default:
                    throw new CaseNotHandledException();
            }

            storeInstruction.Label = instruction.Label;
            translatedInstructions.Add(storeInstruction);
        }

        public void Visit(NopInstruction instruction)
        {
            var basicInstruction = new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.Nop)
            {
                Label = instruction.Label
            };
            translatedInstructions.Add(basicInstruction);
        }

        public void Visit(BreakpointInstruction instruction)
        {
            var basicInstruction = new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.Breakpoint)
            {
                Label = instruction.Label
            };
            translatedInstructions.Add(basicInstruction);
        }

        public void Visit(TryInstruction instruction)
        {
            // try with multiple handlers is modelled as multiple consecutive try instructions with different handlers.
            var previousInstructionIsTry = Index > 0 && method.Body.Instructions[Index - 1] is TryInstruction;
            if (previousInstructionIsTry)
            {
                exceptionInformationBuilder.IncrementCurrentProtectedBlockExpectedHandlers();
            }
            else
            {
                // try starts at the instruction following the try instruction. 
                var label = "";
                for (var i = Index + 1; i < method.Body.Instructions.Count; i++)
                {
                    var currentInstruction = method.Body.Instructions[i];
                    if (!(currentInstruction is TryInstruction))
                    {
                        label = currentInstruction.Label;
                        break;
                    }
                }

                exceptionInformationBuilder.BeginProtectedBlockAt(label);
            }
        }

        public void Visit(FaultInstruction instruction)
        {
            var label = method.Body.Instructions[Index + 1].Label;
            exceptionInformationBuilder.AddHandlerToCurrentProtectedBlock(label, ExceptionHandlerBlockKind.Fault);
        }

        public void Visit(FinallyInstruction instruction)
        {
            var label = method.Body.Instructions[Index + 1].Label;
            exceptionInformationBuilder.AddHandlerToCurrentProtectedBlock(label, ExceptionHandlerBlockKind.Finally);
        }


        public void Visit(FilterInstruction instruction)
        {
            var label = method.Body.Instructions[Index + 1].Label;
            exceptionInformationBuilder.AddFilterHandlerToCurrentProtectedBlock(label, instruction.kind, instruction.ExceptionType);
        }

        public void Visit(CatchInstruction instruction)
        {
            var label = method.Body.Instructions[Index + 1].Label;
            exceptionInformationBuilder.AddHandlerToCurrentProtectedBlock(label, ExceptionHandlerBlockKind.Catch, instruction.ExceptionType);
        }

        public void Visit(ThrowInstruction instruction)
        {
            var basicInstruction = instruction.HasOperand
                ? new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.Throw)
                : new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.Rethrow);
            basicInstruction.Label = instruction.Label;
            translatedInstructions.Add(basicInstruction);
            if (Index < method.Body.Instructions.Count - 1) //not last instruction. Throw can be the last instruction and not inside a protected block
            {
                var label = method.Body.Instructions[Index + 1].Label;
                exceptionInformationBuilder.EndCurrentProtectedBlockIfAppliesAt(label); // block ends after instruction
            }
        }

        public void Visit(UnconditionalBranchInstruction instruction)
        {
            Bytecode.Instruction bytecodeInstruction;
            switch (instruction.Operation)
            {
                case UnconditionalBranchOperation.Leave:
                {
                    bytecodeInstruction = new Bytecode.BranchInstruction(instruction.Offset, Bytecode.BranchOperation.Leave, instruction.Target);
                    var label = method.Body.Instructions[Index + 1].Label;
                    exceptionInformationBuilder.EndCurrentProtectedBlockIfAppliesAt(label); // block ends after instruction

                    break;
                }
                case UnconditionalBranchOperation.Branch:
                    bytecodeInstruction = new Bytecode.BranchInstruction(instruction.Offset, Bytecode.BranchOperation.Branch, instruction.Target);
                    break;
                case UnconditionalBranchOperation.EndFinally:
                {
                    bytecodeInstruction = new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.EndFinally);
                    // no more handlers after finally
                    var label = method.Body.Instructions[Index + 1].Label;
                    exceptionInformationBuilder.EndCurrentProtectedBlockAt(label); // block ends after instruction 
                    break;
                }
                case UnconditionalBranchOperation.EndFilter:
                    // nothing is done with exceptionInformation since filter area is the gap between try end and handler start
                    bytecodeInstruction = new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.EndFilter);
                    break;
                default:
                    throw instruction.Operation.ToUnknownValueException();
            }

            bytecodeInstruction.Label = instruction.Label;
            translatedInstructions.Add(bytecodeInstruction);
        }

        public void Visit(ConvertInstruction instruction)
        {
            var convertInstruction = new Bytecode.ConvertInstruction(
                instruction.Offset,
                OperationHelper.ToConvertOperation(instruction.Operation),
                instruction.ConversionType)
            {
                Label = instruction.Label,
                OverflowCheck = instruction.OverflowCheck,
                UnsignedOperands = instruction.UnsignedOperands
            };
            translatedInstructions.Add(convertInstruction);
        }

        public void Visit(ReturnInstruction instruction)
        {
            var basicInstruction = new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.Return)
            {
                Label = instruction.Label
            };
            translatedInstructions.Add(basicInstruction);
        }

        public void Visit(ConditionalBranchInstruction instruction)
        {
            var branchOperation = OperationHelper.ToBranchOperation(instruction.Operation);
            if (instruction.RightOperand is Constant constant)
            {
                var loadInstruction = new Bytecode.LoadInstruction(instruction.Offset, Bytecode.LoadOperation.Value, constant)
                {
                    Label = instruction.Label + "º" // ensure unique label
                };
                translatedInstructions.Add(loadInstruction);
            }
            var branchInstruction = new Bytecode.BranchInstruction(instruction.Offset, branchOperation, instruction.Target)
            {
                Label = instruction.Label
            };
            translatedInstructions.Add(branchInstruction);
        }

        public void Visit(SwitchInstruction instruction)
        {
            var switchInstruction = new Bytecode.SwitchInstruction(instruction.Offset, instruction.Targets)
            {
                Label = instruction.Label
            };
            translatedInstructions.Add(switchInstruction);
        }

        public void Visit(SizeofInstruction instruction)
        {
            var sizeofInstruction = new Bytecode.SizeofInstruction(instruction.Offset, instruction.MeasuredType)
            {
                Label = instruction.Label
            };
            translatedInstructions.Add(sizeofInstruction);
        }

        public void Visit(LoadTokenInstruction instruction)
        {
            var loadTokenInstruction = new Bytecode.LoadTokenInstruction(instruction.Offset, instruction.Token)
            {
                Label = instruction.Label
            };
            translatedInstructions.Add(loadTokenInstruction);
        }

        public void Visit(MethodCallInstruction instruction)
        {
            var methodCallInstruction = new Bytecode.MethodCallInstruction(
                instruction.Offset,
                OperationHelper.ToMethodCallOperation(instruction.Operation),
                instruction.Method
            )
            {
                Label = instruction.Label
            };
            translatedInstructions.Add(methodCallInstruction);
        }

        public void Visit(IndirectMethodCallInstruction instruction)
        {
            var indirectMethodCallInstruction = new Bytecode.IndirectMethodCallInstruction(instruction.Offset, instruction.Function)
            {
                Label = instruction.Label
            };
            translatedInstructions.Add(indirectMethodCallInstruction);
        }

        public void Visit(CreateObjectInstruction instruction)
        {
            var methodCallInstruction = (MethodCallInstruction) method.Body.Instructions[Index + 1];

            // do not translate following method call and load instruction
            ignoredInstructions.Add(Index + 1); // method call
            ignoredInstructions.Add(Index + 2); // load

            var createObjectInstruction = new Bytecode.CreateObjectInstruction(instruction.Offset, methodCallInstruction.Method)
            {
                Label = instruction.Label
            };
            translatedInstructions.Add(createObjectInstruction);
        }

        public void Visit(CopyMemoryInstruction instruction)
        {
            var basicInstruction = new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.CopyBlock)
            {
                Label = instruction.Label
            };
            translatedInstructions.Add(basicInstruction);
        }

        public void Visit(LocalAllocationInstruction instruction)
        {
            var basicInstruction = new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.LocalAllocation)
            {
                Label = instruction.Label
            };
            translatedInstructions.Add(basicInstruction);
        }

        public void Visit(InitializeMemoryInstruction instruction)
        {
            var basicInstruction = new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.InitBlock)
            {
                Label = instruction.Label
            };
            translatedInstructions.Add(basicInstruction);
        }

        public void Visit(InitializeObjectInstruction instruction)
        {
            var type = ((PointerType) instruction.TargetAddress.Type).TargetType;
            var initObjInstruction = new Bytecode.InitObjInstruction(instruction.Offset, type)
            {
                Label = instruction.Label
            };
            translatedInstructions.Add(initObjInstruction);
        }

        public void Visit(CopyObjectInstruction instruction)
        {
            var basicInstruction = new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.CopyObject)
            {
                Label = instruction.Label
            };
            translatedInstructions.Add(basicInstruction);
        }

        public void Visit(CreateArrayInstruction instruction)
        {
            var arrayType = new ArrayType(instruction.ElementType, instruction.Rank);
            var createArrayInstruction = new Bytecode.CreateArrayInstruction(instruction.Offset, arrayType)
            {
                Label = instruction.Label,
                WithLowerBound = instruction.LowerBounds.Any(),
                Constructor = instruction.Constructor
            };
            translatedInstructions.Add(createArrayInstruction);
        }

        public void Visit(PhiInstruction instruction) => throw new CaseNotHandledException();

        public void Visit(ConstrainedInstruction instruction)
        {
            var constrainedInstruction = new Bytecode.ConstrainedInstruction(instruction.Offset, instruction.ThisType)
            {
                Label = instruction.Label
            };
            translatedInstructions.Add(constrainedInstruction);
        }

        public void Visit(PopInstruction instruction)
        {
            var basicInstruction = new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.Pop);
            translatedInstructions.Add(basicInstruction);
        }

        // FIXME se podria ir calculando. Pero ahi hay que ir contando segun la instruccion (si suma o vacia el stack). Es esta la mejor forma?
        private class StackSize
        {
            private uint currentStackSize;
            public uint MaxStackSize { get; private set; }

            public StackSize()
            {
                currentStackSize = 0;
                MaxStackSize = 0;
            }

            public void Increment()
            {
                currentStackSize += 1;
                if (currentStackSize > MaxStackSize)
                {
                    MaxStackSize = currentStackSize;
                }
            }

            public void Decrement()
            {
                if (currentStackSize == 0) throw new Exception("Current stack size is 0");
                currentStackSize -= 1;
            }

            public void Clear()
            {
                currentStackSize = 0;
            }
        }

        private class CaseNotHandledException : Exception
        {
        }
    }
}
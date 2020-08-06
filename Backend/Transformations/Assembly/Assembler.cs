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

// FIXME revisar los usos de operand y result. Cuando hay que usar uno y cuando otro? lo mismo lo de mirar si son temporal variables y eso
// FIXME me suena a que esta mal
// FIXME reveer ademas toda la parte de las local variables y demas. La traduccion no tiene que dejar el bytecod eigual al original, sino 
// FIXME semanticamente igual. Por ende no tengo porque tener las mismas variables que antes. Puede haber mas, puede haber menos.
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

            body.MaxStack = 10; // FIXME calcular (ver StackSize)
            body.Parameters.AddRange(method.Body.Parameters);

            if (method.Body.Instructions.Count > 0)
            {
                var instructionTranslator = new InstructionTranslator(method.Body);
                instructionTranslator.Visit(method.Body);

                body.ExceptionInformation.AddRange(instructionTranslator.exceptionInformationBuilder.Build());
                body.Instructions.AddRange(instructionTranslator.translatedInstructions);

                body.UpdateVariables();
            }

            return body;
        }

        private class InstructionTranslator : InstructionVisitor
        {
            // FIXMe hay que ver si no es mas prolijo quiza que sean del  y que se las pase para que las rellene, mas que que este las tenga public
            public readonly IList<Bytecode.Instruction> translatedInstructions = new List<Bytecode.Instruction>();
            public readonly ExceptionInformationBuilder exceptionInformationBuilder = new ExceptionInformationBuilder();
            public readonly StackSize stackSize;
/*
            FIXME
            Tema maxStack. quiza convenga validar con edgar como hacerlo. Porque ya veo que quiza hay que usar el CFG en vez de recorrer asi para calcularlo
            ver como lo calcula en el dissasembler (eso de stack size at entry y demas)
            */
            
            private readonly MethodBody bodyToProcess;
            private readonly IDictionary<int, bool> ignoreInstruction = new Dictionary<int, bool>();

            public InstructionTranslator(MethodBody bodyToProcess)
            {
                this.bodyToProcess = bodyToProcess;
                stackSize = new StackSize();
            }

            public override bool ShouldVisit(Instruction instruction)
            {
                var shouldProcessInstruction = !ignoreInstruction.TryGetValue(bodyToProcess.Instructions.IndexOf(instruction), out _);
                return shouldProcessInstruction;
            }

            public override void Visit(PopInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.Pop) {Label = instruction.Label};
                translatedInstructions.Add(basicInstruction);
            }

            public override void Visit(BinaryInstruction instruction)
            {
                // FIXME tiene sentido esto?
                if (instruction.Operation == BinaryOperation.Neq)
                {
                    var basicInstruction = new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.Eq)
                    {
                        Label = instruction.Label
                    };
                    translatedInstructions.Add(basicInstruction);
                    basicInstruction = new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.Neg)
                    {
                        Label = instruction.Label
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

            public override void Visit(UnaryInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(instruction.Offset, OperationHelper.ToBasicOperation(instruction.Operation))
                {
                    Label = instruction.Label
                };
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

                bytecodeInstruction.Label = instruction.Label;
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
                        {
                            Method = arrayElementAccess.Method
                        };
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

                storeInstruction.Label = instruction.Label;
                translatedInstructions.Add(storeInstruction);
            }

            public override void Visit(NopInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.Nop)
                {
                    Label = instruction.Label
                };
                translatedInstructions.Add(basicInstruction);
            }

            public override void Visit(BreakpointInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.Breakpoint)
                {
                    Label = instruction.Label
                };
                translatedInstructions.Add(basicInstruction);
            }

            public override void Visit(TryInstruction instruction)
            {
                // try with multiple handlers is modelled as multiple consecutive try instructions with different handlers.
                var instructionIndex = bodyToProcess.Instructions.IndexOf(instruction);
                var previousInstructionIsTry = instructionIndex > 0 && bodyToProcess.Instructions[instructionIndex - 1] is TryInstruction;
                if (previousInstructionIsTry)
                {
                    exceptionInformationBuilder.IncrementCurrentProtectedBlockExpectedHandlers();
                }
                else
                {
                    // try starts at the instruction following the try instruction. 
                    var label = "";
                    for (var i = instructionIndex + 1; i < bodyToProcess.Instructions.Count; i++)
                    {
                        var currentInstruction = bodyToProcess.Instructions[i];
                        if (!(currentInstruction is TryInstruction))
                        {
                            label = currentInstruction.Label;
                            break;
                        }
                    }

                    exceptionInformationBuilder.BeginProtectedBlockAt(label);
                }
            }

            public override void Visit(FaultInstruction instruction)
            {
                // FIXME comment, esta duplicado en todos ademas. Hay una form amas eficiente de hacer esto? 
                var index = bodyToProcess.Instructions.IndexOf(instruction);
                var label = bodyToProcess.Instructions[index + 1].Label;
                exceptionInformationBuilder.AddHandlerToCurrentProtectedBlock(label, ExceptionHandlerBlockKind.Fault);
            }

            public override void Visit(FinallyInstruction instruction)
            {
                var index = bodyToProcess.Instructions.IndexOf(instruction);
                var label = bodyToProcess.Instructions[index + 1].Label;
                exceptionInformationBuilder.AddHandlerToCurrentProtectedBlock(label, ExceptionHandlerBlockKind.Finally);
            }


            public override void Visit(FilterInstruction instruction)
            {
                var index = bodyToProcess.Instructions.IndexOf(instruction);
                var label = bodyToProcess.Instructions[index + 1].Label;
                exceptionInformationBuilder.AddFilterHandlerToCurrentProtectedBlock(label, instruction.kind, instruction.ExceptionType);
            }

            public override void Visit(CatchInstruction instruction)
            {
                var index = bodyToProcess.Instructions.IndexOf(instruction);
                var label = bodyToProcess.Instructions[index + 1].Label;
                exceptionInformationBuilder.AddHandlerToCurrentProtectedBlock(
                    label,
                    ExceptionHandlerBlockKind.Catch,
                    instruction.ExceptionType);
            }

            public override void Visit(ThrowInstruction instruction)
            {
                var basicInstruction = instruction.HasOperand
                    ? new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.Throw)
                    : new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.Rethrow);
                basicInstruction.Label = instruction.Label;
                translatedInstructions.Add(basicInstruction);
                if (!bodyToProcess.Instructions.Last().Equals(instruction)) // FIXME medio choto esto
                {
                    var index = bodyToProcess.Instructions.IndexOf(instruction);
                    var label = bodyToProcess.Instructions[index + 1].Label;
                    exceptionInformationBuilder.EndCurrentProtectedBlockIfAppliesAt(label); // block ends after instruction
                }
            }

            public override void Visit(UnconditionalBranchInstruction instruction)
            {
                var target = Convert.ToUInt32(instruction.Target.Substring(2), 16);
                Bytecode.Instruction bytecodeInstruction;
                switch (instruction.Operation)
                {
                    case UnconditionalBranchOperation.Leave:
                    {
                        bytecodeInstruction = new Bytecode.BranchInstruction(instruction.Offset, Bytecode.BranchOperation.Leave, target);
                        var index = bodyToProcess.Instructions.IndexOf(instruction);
                        var label = bodyToProcess.Instructions[index + 1].Label;
                        exceptionInformationBuilder.EndCurrentProtectedBlockIfAppliesAt(label); // block ends after instruction

                        break;
                    }
                    case UnconditionalBranchOperation.Branch:
                    {
                        bytecodeInstruction = new Bytecode.BranchInstruction(instruction.Offset, Bytecode.BranchOperation.Branch, target);

                        break;
                    }
                    case UnconditionalBranchOperation.EndFinally:
                    {
                        bytecodeInstruction = new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.EndFinally);
                        // no more handlers after finally
                        var index = bodyToProcess.Instructions.IndexOf(instruction);
                        var label = bodyToProcess.Instructions[index + 1].Label;
                        exceptionInformationBuilder.EndCurrentProtectedBlockAt(label); // block ends after instruction 
                        break;
                    }
                    case UnconditionalBranchOperation.EndFilter:
                    {
                        // nothing is done with exceptionInformation since filter area is the gap between try end and handler start
                        bytecodeInstruction = new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.EndFilter);
                        break;
                    }
                    default: throw instruction.Operation.ToUnknownValueException();
                }

                bytecodeInstruction.Label = instruction.Label;
                translatedInstructions.Add(bytecodeInstruction);
            }

            public override void Visit(ConvertInstruction instruction)
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

            public override void Visit(ReturnInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.Return)
                {
                    Label = instruction.Label
                };
                translatedInstructions.Add(basicInstruction);
            }

            public override void Visit(ConditionalBranchInstruction instruction)
            {
                // FIXME esto se puede optimizar. Porque esto hace que por ejemplo haga un load false y despues haga un branc eq. EN vez
                // FIXME de de una hacer brfalse. Por otro lado, agarro solo los constant (literales) porque los demas casos son variables y se supone
                // FIXME que ya se cargaron en la instruccion anterior
                switch (instruction.RightOperand)
                {
                    case Constant constant:
                        var a = new Bytecode.LoadInstruction(instruction.Offset, Bytecode.LoadOperation.Value, constant);
                        a.Label += "'"; // FIXME
                        translatedInstructions.Add(a);
                        break;
                }

                var target = Convert.ToUInt32(instruction.Target.Substring(2), 16);
                var branchInstruction = new Bytecode.BranchInstruction(
                    instruction.Offset,
                    OperationHelper.ToBranchOperation(instruction.Operation),
                    target)
                {
                    Label = instruction.Label
                };
                translatedInstructions.Add(branchInstruction);
            }

            public override void Visit(SwitchInstruction instruction)
            {
                var targets = instruction.Targets
                    .Select(target => Convert.ToUInt32(target.Substring(2), 16))
                    .ToList();
                var switchInstruction = new Bytecode.SwitchInstruction(instruction.Offset, targets)
                {
                    Label = instruction.Label
                };
                translatedInstructions.Add(switchInstruction);
            }

            public override void Visit(SizeofInstruction instruction)
            {
                var sizeofInstruction = new Bytecode.SizeofInstruction(instruction.Offset, instruction.MeasuredType)
                {
                    Label = instruction.Label
                };
                translatedInstructions.Add(sizeofInstruction);
            }

            public override void Visit(LoadTokenInstruction instruction)
            {
                var loadTokenInstruction = new Bytecode.LoadTokenInstruction(instruction.Offset, instruction.Token)
                {
                    Label = instruction.Label
                };
                translatedInstructions.Add(loadTokenInstruction);
            }

            public override void Visit(MethodCallInstruction instruction)
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

            public override void Visit(IndirectMethodCallInstruction instruction)
            {
                var indirectMethodCallInstruction = new Bytecode.IndirectMethodCallInstruction(instruction.Offset, instruction.Function)
                {
                    Label = instruction.Label
                };
                translatedInstructions.Add(indirectMethodCallInstruction);
            }

            public override void Visit(CreateObjectInstruction instruction)
            {
                var index = bodyToProcess.Instructions.IndexOf(instruction);
                var methodCallInstruction = (MethodCallInstruction) bodyToProcess.Instructions[index + 1];
                ignoreInstruction.Add(index + 1, true); // method call
                ignoreInstruction.Add(index + 2, true); // load

                var createObjectInstruction = new Bytecode.CreateObjectInstruction(instruction.Offset, methodCallInstruction.Method)
                {
                    Label = instruction.Label
                };
                translatedInstructions.Add(createObjectInstruction);
            }

            public override void Visit(CopyMemoryInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.CopyBlock)
                {
                    Label = instruction.Label
                };
                translatedInstructions.Add(basicInstruction);
            }

            public override void Visit(LocalAllocationInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.LocalAllocation)
                {
                    Label = instruction.Label
                };
                translatedInstructions.Add(basicInstruction);
            }

            public override void Visit(InitializeMemoryInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.InitBlock)
                {
                    Label = instruction.Label
                };
                translatedInstructions.Add(basicInstruction);
            }

            public override void Visit(InitializeObjectInstruction instruction)
            {
                var initObjInstruction = new Bytecode.InitObjInstruction(instruction.Offset, instruction.TargetAddress.Type)
                {
                    Label = instruction.Label
                };
                translatedInstructions.Add(initObjInstruction);
            }

            public override void Visit(CopyObjectInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(instruction.Offset, Bytecode.BasicOperation.CopyObject)
                {
                    Label = instruction.Label
                };
                translatedInstructions.Add(basicInstruction);
            }

            public override void Visit(CreateArrayInstruction instruction)
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

            public override void Visit(PhiInstruction instruction) => throw new Exception();

            public override void Visit(ConstrainedInstruction instruction)
            {
                var constrainedInstruction = new Bytecode.ConstrainedInstruction(instruction.Offset, instruction.ThisType)
                {
                    Label = instruction.Label
                };
                translatedInstructions.Add(constrainedInstruction);
            }
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
    }
}
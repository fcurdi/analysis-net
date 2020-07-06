using System;
using System.Collections.Generic;
using System.Linq;
using Backend.Utils;
using Model;
using Model.ThreeAddressCode.Instructions;
using Model.ThreeAddressCode.Values;
using Model.ThreeAddressCode.Visitor;
using Model.Types;
using BranchInstruction = Model.ThreeAddressCode.Instructions.BranchInstruction;
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

            public override bool ShouldVisit(Instruction instruction)
            {
                var shouldProcessInstruction = !ignoreInstruction.TryGetValue(bodyToProcess.Instructions.IndexOf(instruction), out _);
                return shouldProcessInstruction;
            }

            public override void Visit(PopInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(offset, Bytecode.BasicOperation.Pop);
                translatedInstructions.Add(basicInstruction);
                offset++; // 1 Byte OpCode
            }

            public override void Visit(BinaryInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(offset, OperationHelper.ToBasicOperation(instruction.Operation))
                {
                    OverflowCheck = instruction.OverflowCheck,
                    UnsignedOperands = instruction.UnsignedOperands
                };
                translatedInstructions.Add(basicInstruction);
                switch (basicInstruction.Operation)
                {
                    case Bytecode.BasicOperation.Gt:
                    case Bytecode.BasicOperation.Lt:
                    case Bytecode.BasicOperation.Eq:
                        offset += 2; // 2 Byte OpCode
                        break;
                    default:
                        offset++; // 1 Byte OpCode
                        break;
                }
            }

            public override void Visit(UnaryInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(offset, OperationHelper.ToBasicOperation(instruction.Operation));
                translatedInstructions.Add(basicInstruction);
                offset++; // 1 Byte OpCode
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
                        offset++;
                    }
                }
                else
                {
                    if (instruction.Result is LocalVariable loc)
                    {
                        bytecodeInstruction = new Bytecode.StoreInstruction(offset, loc);
                        if (loc.IsParameter)
                        {
                            // starg num -> FE 0B <unsigned int16> (2 + 2) 
                            // starg.s num -> 10 <unsigned int8> (1 + 1)
                            offset += (uint) (loc.Index > byte.MaxValue ? 4 : 2);
                        }
                        else
                        {
                            switch (loc.Index)
                            {
                                case 0:
                                case 1:
                                case 2:
                                case 3:
                                    offset++; // 1byte OpCode
                                    break;
                                default:
                                    // stloc indx -> FE 0E <unsigned int16> (2 + 2) 
                                    // stloc.s indx -> 13 <unsigned int8> (1 + 1) 
                                    offset += (uint) (loc.Index > byte.MaxValue ? 4 : 2);
                                    break;
                            }
                        }
                    }
                    else
                    {
                        switch (instruction.Operand)
                        {
                            case Constant constant:
                                bytecodeInstruction = new Bytecode.LoadInstruction(offset, Bytecode.LoadOperation.Value, constant);
                                switch (constant.Value)
                                {
                                    case null:
                                        offset++; // ldnull -> 14 (1)
                                        break;
                                    case string _:
                                        offset += 5; // ldstr string -> 72 <T> (1 + 4)  
                                        break;
                                    case -1:
                                    case 0:
                                    case 1:
                                    case 2:
                                    case 3:
                                    case 4:
                                    case 5:
                                    case 6:
                                    case 7:
                                    case 8:
                                        offset++;
                                        break;
                                    case object _ when constant.Type.IsOneOf(PlatformTypes.Int8, PlatformTypes.Int16, PlatformTypes.Int32):
                                    {
                                        var value = (int) constant.Value;
                                        if (value >= sbyte.MinValue && value <= sbyte.MaxValue)
                                        {
                                            offset += 2; // ldc.i4.s num -> 1F <int8> (1 + 1)
                                        }
                                        else
                                        {
                                            offset += 5; // ldc.i4 num -> 20 <int32> (1 + 4)
                                        }

                                        break;
                                    }
                                    case object _ when constant.Type.Equals(PlatformTypes.Int64):
                                        offset += 9; // ldc.i8 num-> 21 <int64> (1 + 8)
                                        break;
                                    case object _ when constant.Type.IsOneOf(PlatformTypes.UInt8, PlatformTypes.UInt16, PlatformTypes.UInt32):
                                    {
                                        var value = (uint) constant.Value;
                                        if (value <= byte.MaxValue)
                                        {
                                            offset += 2; // ldc.i4.s num -> 1F <int8> (1 + 1)
                                        }
                                        else
                                        {
                                            offset += 5; // ldc.i4 num -> 20 <int32> (1 + 4)
                                        }

                                        break;
                                    }
                                    case object _ when constant.Type.Equals(PlatformTypes.UInt64):
                                        offset += 9; // ldc.i8 num-> 21 <int64> (1 + 8)
                                        break;

                                    case object _ when constant.Type.Equals(PlatformTypes.Float32):
                                        offset += 5; // ldc.r4 num -> 22 <float32> (1 + 4)
                                        break;
                                    case object _ when constant.Type.Equals(PlatformTypes.Float64):
                                    {
                                        offset += 9; // ldc.r8 num -> 23 <float64> (1 + 8)
                                        break;
                                    }
                                    default:
                                        throw new Exception();
                                }


                                break;
                            case LocalVariable localVariable:
                            {
                                bytecodeInstruction = new Bytecode.LoadInstruction(offset, Bytecode.LoadOperation.Content, localVariable);
                                switch (localVariable.Index)
                                {
                                    // ldloc.0,1,2,3 ldarg.0,1,2,3
                                    case 0:
                                    case 1:
                                    case 2:
                                    case 3:
                                        offset++;
                                        break;
                                    default:
                                        // ldloc indx -> FE 0C <unsigned int16>  (2 + 2) 
                                        // ldloc.s indx -> 11 <unsigned int8> (1 + 1) 
                                        // ldarg num -> FE 09 <unsigned int16> (2 + 2) 
                                        // ldarg.s num -> 0E <unsigned int8>  (1 + 1)
                                        offset += (uint) (localVariable.Index > byte.MaxValue ? 4 : 2);
                                        break;
                                }

                                break;
                            }
                            case Dereference dereference:
                            {
                                var type = dereference.Type;
                                bytecodeInstruction = new Bytecode.LoadIndirectInstruction(offset, type);
                                if (type.IsIntType() || type.IsFloatType() || type.Equals(PlatformTypes.IntPtr) || type.Equals(PlatformTypes.Object))
                                {
                                    // 1 byte opcode
                                    offset++;
                                }
                                else
                                {
                                    // ldobj typeTok -> 71 <Token> (1 + 4) 
                                    offset += 5;
                                }

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

                                        // ldelema typeTok -> 8F <Token> (1 + 4) 
                                        // ldobj typeTok -> 71 <Token> (1 + 4)
                                        offset += 5;
                                        break;
                                    }

                                    case LocalVariable localVariable:
                                    {
                                        bytecodeInstruction = new Bytecode.LoadInstruction(offset, Bytecode.LoadOperation.Address, localVariable);
                                        // ldloca indx -> FE 0D <unsigned int16>  (2 + 2) 
                                        // ldloca.s indx -> 12 <unsigned int8> (1 + 1)
                                        // ldarga argNum -> FE 0A <unsigned int16> (2 + 2)
                                        // ldarga.s argNum -> 0F <unsigned int8> (1 + 1)
                                        offset += (uint) (localVariable.Index > byte.MaxValue ? 4 : 2);
                                        break;
                                    }
                                    case InstanceFieldAccess instanceFieldAccess:
                                        bytecodeInstruction = new Bytecode.LoadFieldInstruction(
                                            offset,
                                            Bytecode.LoadFieldOperation.Address,
                                            instanceFieldAccess.Field);
                                        // ldsflda field -> 0x7F <Token> (1 + 4)
                                        // ldflda field -> 0x7C <Token>  (1 + 4)
                                        offset += 5;
                                        break;
                                    default:
                                        throw new Exception(); // TODO
                                }

                                break;
                            case ArrayLengthAccess _:
                                bytecodeInstruction = new Bytecode.BasicInstruction(offset, Bytecode.BasicOperation.LoadArrayLength);
                                offset++;
                                break;
                            case VirtualMethodReference virtualMethodReference:
                                bytecodeInstruction = new Bytecode.LoadMethodAddressInstruction(
                                    offset,
                                    Bytecode.LoadMethodAddressOperation.Virtual,
                                    virtualMethodReference.Method);
                                // ldvirtftn method -> FE 07 <Token> (2 + 4)  
                                offset += 6;
                                break;
                            case StaticMethodReference staticMethodReference:
                                bytecodeInstruction = new Bytecode.LoadMethodAddressInstruction(
                                    offset,
                                    Bytecode.LoadMethodAddressOperation.Static,
                                    staticMethodReference.Method);
                                // ldftn method -> FE 06 <Token> (2 + 4)  
                                offset += 6;
                                break;
                            case InstanceFieldAccess instanceFieldAccess:
                                bytecodeInstruction = new Bytecode.LoadFieldInstruction(
                                    offset,
                                    Bytecode.LoadFieldOperation.Content,
                                    instanceFieldAccess.Field);
                                //  ldfld field -> 7B <T> (1 + 4)
                                offset += 5;
                                break;
                            case StaticFieldAccess staticFieldAccess:
                                bytecodeInstruction = new Bytecode.LoadFieldInstruction(
                                    offset,
                                    Bytecode.LoadFieldOperation.Content,
                                    staticFieldAccess.Field);
                                // ldsfld field -> 7E <T> (1 + 4)
                                offset += 5;
                                break;
                            case ArrayElementAccess arrayElementAccess:
                            {
                                var type = (ArrayType) arrayElementAccess.Array.Type;
                                bytecodeInstruction = new Bytecode.LoadArrayElementInstruction(
                                    offset,
                                    Bytecode.LoadArrayElementOperation.Content,
                                    type) {Method = arrayElementAccess.Method};

                                if (type.IsVector &&
                                    (type.ElementsType.IsIntType() ||
                                     type.ElementsType.IsFloatType() ||
                                     type.ElementsType.Equals(PlatformTypes.IntPtr) ||
                                     type.ElementsType.Equals(PlatformTypes.Object)))
                                {
                                    // 1 byte opcode
                                    offset++;
                                }
                                else
                                {
                                    // ldelem typeTok -> A3 <Token> (1 + 4) 
                                    offset += 5;
                                }

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
                        storeInstruction = new Bytecode.StoreArrayElementInstruction(offset, type) {Method = arrayElementAccess.Method};
                        if (type.IsVector &&
                            (type.ElementsType.IsIntType() ||
                             type.ElementsType.IsFloatType() ||
                             type.ElementsType.Equals(PlatformTypes.IntPtr) ||
                             type.ElementsType.Equals(PlatformTypes.Object)))
                        {
                            // 1 byte opcode
                            offset++;
                        }
                        else
                        {
                            // stelem typeTok -> A4 <Token> (1 + 4) 
                            offset += 5;
                        }

                        break;
                    }
                    case Dereference dereference:
                    {
                        var type = dereference.Type;
                        storeInstruction = new Bytecode.StoreIndirectInstruction(offset, dereference.Type);
                        if (type.IsIntType() || type.IsFloatType() || type.Equals(PlatformTypes.IntPtr) || type.Equals(PlatformTypes.Object))
                        {
                            // 1 byte opcode
                            offset++;
                        }
                        else
                        {
                            // stobj typeTok -> 81 <Token> (1 + 4) 
                            offset += 5;
                        }

                        break;
                    }
                    case InstanceFieldAccess instanceFieldAccess:
                        storeInstruction = new Bytecode.StoreFieldInstruction(offset, instanceFieldAccess.Field);
                        // stfld field -> 7D <T> (1 + 4)
                        offset += 5;
                        break;
                    case StaticFieldAccess staticFieldAccess:
                        storeInstruction = new Bytecode.StoreFieldInstruction(offset, staticFieldAccess.Field);
                        // stsfld field -> 80 <T> (1 + 4)
                        offset += 5;
                        break;
                    default:
                        throw new Exception(); // TODO msg
                }

                translatedInstructions.Add(storeInstruction);
            }

            public override void Visit(NopInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(offset, Bytecode.BasicOperation.Nop);
                translatedInstructions.Add(basicInstruction);
                offset++; // 1 Byte OpCode
            }

            public override void Visit(BreakpointInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(offset, Bytecode.BasicOperation.Breakpoint);
                translatedInstructions.Add(basicInstruction);
                offset++; // 1 Byte OpCode
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
                Bytecode.BasicInstruction basicInstruction;
                if (instruction.HasOperand)
                {
                    basicInstruction = new Bytecode.BasicInstruction(offset, Bytecode.BasicOperation.Throw);
                    offset++; // 1 Byte OpCode
                }
                else
                {
                    basicInstruction = new Bytecode.BasicInstruction(offset, Bytecode.BasicOperation.Rethrow);
                    offset += 2; // 2 Byte OpCode
                }

                exceptionInformationBuilder.EndCurrentProtectedBlockIfAppliesAt(offset);
                translatedInstructions.Add(basicInstruction);
            }

            public override void Visit(UnconditionalBranchInstruction instruction)
            {
                var target = Convert.ToUInt32(instruction.Target.Substring(2), 16);
                Bytecode.Instruction branchInstruction;
                switch (instruction.Operation)
                {
                    case UnconditionalBranchOperation.Leave:
                    {
                        branchInstruction = new Bytecode.BranchInstruction(offset, Bytecode.BranchOperation.Leave, target);
                        // leave target -> DD <int32> (1 + 4)
                        // leave.s target -> DE <int8> (1 + 1)
                        offset += (uint) (IsShortForm(instruction) ? 2 : 5);
                        exceptionInformationBuilder.EndCurrentProtectedBlockIfAppliesAt(offset);
                        break;
                    }
                    case UnconditionalBranchOperation.Branch:
                    {
                        branchInstruction = new Bytecode.BranchInstruction(offset, Bytecode.BranchOperation.Branch, target);
                        // leave target -> 38 <int32> (1 + 4)
                        // leave.s target -> 2B <int8> (1 + 1)
                        offset += (uint) (IsShortForm(instruction) ? 2 : 5);
                        break;
                    }
                    case UnconditionalBranchOperation.EndFinally:
                    {
                        branchInstruction = new Bytecode.BasicInstruction(offset, Bytecode.BasicOperation.EndFinally);
                        offset++; // 1 Byte OpCode
                        exceptionInformationBuilder.EndCurrentProtectedBlockAt(offset); // no more handlers after finally
                        break;
                    }
                    case UnconditionalBranchOperation.EndFilter:
                    {
                        // nothing is done with exceptionInformation since filter area is the gap between try end and handler start
                        branchInstruction = new Bytecode.BasicInstruction(offset, Bytecode.BasicOperation.EndFilter);
                        offset += 2; // 2 Byte OpCode
                        break;
                    }
                    default: throw instruction.Operation.ToUnknownValueException();
                }

                translatedInstructions.Add(branchInstruction);
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
                translatedInstructions.Add(convertInstruction);
                switch (convertInstruction.Operation)
                {
                    case Bytecode.ConvertOperation.Conv:
                        offset++; // 1 Byte OpCode
                        break;
                    default:
                        // box typeTok -> 8C <Token> (1 + 4) 
                        // castclass typeTok -> 74 <Token> (1 + 4) 
                        // isinst typeTok -> 75 <Token> (1 + 4) 
                        // unbox valuetype -> 79 <Token> (1 + 4) 
                        // unbox.any typeTok -> A5 <Token> (1 + 4) 
                        offset += 5;
                        break;
                }
            }

            public override void Visit(ReturnInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(offset, Bytecode.BasicOperation.Return);
                translatedInstructions.Add(basicInstruction);
                offset++; // 1 Byte OpCode
            }

            public override void Visit(ConditionalBranchInstruction instruction)
            {
                var target = Convert.ToUInt32(instruction.Target.Substring(2), 16);
                var branchInstruction = new Bytecode.BranchInstruction(
                    offset,
                    OperationHelper.ToBranchOperation(instruction.Operation, instruction.RightOperand),
                    target);
                translatedInstructions.Add(branchInstruction);
                // br* target -> 1ByteOpcode <int32> (1 + 4)
                // br*.s target -> 1ByteOpcode <int8> (1 + 1)
                offset += (uint) (IsShortForm(instruction) ? 2 : 5);
            }

            public override void Visit(SwitchInstruction instruction)
            {
                var targets = instruction.Targets.Select(target => Convert.ToUInt32(target.Substring(2), 16)).ToList();
                var switchInstruction = new Bytecode.SwitchInstruction(offset, targets);
                translatedInstructions.Add(switchInstruction);
                // switch number t1 t2 t3 ... -> 45 <uint32> <int32> <int32> <int32> .... (1 + 4 + n*4)
                offset += (uint) (1 + 4 + 4 * targets.Count);
            }

            public override void Visit(SizeofInstruction instruction)
            {
                var sizeofInstruction = new Bytecode.SizeofInstruction(offset, instruction.MeasuredType);
                translatedInstructions.Add(sizeofInstruction);
                // sizeof typetok -> FE 1C <Token> (2 + 4)
                offset += 6;
            }

            public override void Visit(LoadTokenInstruction instruction)
            {
                var loadTokenInstruction = new Bytecode.LoadTokenInstruction(offset, instruction.Token);
                translatedInstructions.Add(loadTokenInstruction);
                //  ldtoken token -> D0 <Token> (1 + 4)
                offset += 5;
            }

            public override void Visit(MethodCallInstruction instruction)
            {
                var methodCallInstruction = new Bytecode.MethodCallInstruction(
                    offset,
                    OperationHelper.ToMethodCallOperation(instruction.Operation),
                    instruction.Method
                );
                translatedInstructions.Add(methodCallInstruction);
                // call method -> 28 <Token> (1 + 4)
                // callvirt method -> 6F <Token> (1 + 4)
                offset += 5;
            }

            public override void Visit(IndirectMethodCallInstruction instruction)
            {
                var indirectMethodCallInstruction = new Bytecode.IndirectMethodCallInstruction(offset, instruction.Function);
                translatedInstructions.Add(indirectMethodCallInstruction);
                // calli callsitedescr -> 29 <T> (1 + 4)
                offset += 5;
            }

            public override void Visit(CreateObjectInstruction instruction)
            {
                var index = bodyToProcess.Instructions.IndexOf(instruction);
                var methodCallInstruction = (MethodCallInstruction) bodyToProcess.Instructions[index + 1];
                ignoreInstruction.Add(index + 1, true); // method call
                ignoreInstruction.Add(index + 2, true); // load

                var createObjectInstruction = new Bytecode.CreateObjectInstruction(offset, methodCallInstruction.Method);
                translatedInstructions.Add(createObjectInstruction);
                // newobj ctor -> 73 <Token> (1 + 4)
                offset += 5;
            }

            public override void Visit(CopyMemoryInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(offset, Bytecode.BasicOperation.CopyBlock);
                translatedInstructions.Add(basicInstruction);
                offset += 2; // 2 Byte OpCode
            }

            public override void Visit(LocalAllocationInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(offset, Bytecode.BasicOperation.LocalAllocation);
                translatedInstructions.Add(basicInstruction);
                offset += 2; // 2 Byte OpCode
            }

            public override void Visit(InitializeMemoryInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(offset, Bytecode.BasicOperation.InitBlock);
                translatedInstructions.Add(basicInstruction);
                offset += 2; // 2 Byte OpCode
            }

            public override void Visit(InitializeObjectInstruction instruction)
            {
                var initObjInstruction = new Bytecode.InitObjInstruction(offset, instruction.TargetAddress.Type);
                translatedInstructions.Add(initObjInstruction);
                // initobj typeTok -> FE 15 <Token> (2 + 4)
                offset += 6;
            }

            public override void Visit(CopyObjectInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(offset, Bytecode.BasicOperation.CopyObject);
                translatedInstructions.Add(basicInstruction);
                // cpobj typeTok-> 70 <Token> (1 + 4)
                offset += 5;
            }

            public override void Visit(CreateArrayInstruction instruction)
            {
                var arrayType = new ArrayType(instruction.ElementType, instruction.Rank);
                var createArrayInstruction = new Bytecode.CreateArrayInstruction(offset, arrayType)
                {
                    WithLowerBound = instruction.LowerBounds.Any(),
                    Constructor = instruction.Constructor
                };
                translatedInstructions.Add(createArrayInstruction);
                // newobj ctor -> 73 <Token> (1 + 4)
                // newarr etype -> 8D <Token> (1 + 4)
                offset += 5;
            }

            public override void Visit(PhiInstruction instruction)
            {
                throw new Exception();
            }

            public override void Visit(ConstrainedInstruction instruction)
            {
                translatedInstructions.Add(new Bytecode.ConstrainedInstruction(offset, instruction.ThisType));
                // constrained. thisType -> FE 16 <T> (2 + 4)
                offset += 6;
            }


            // FIXME: duplicate with method body generator
            private bool IsShortForm(BranchInstruction instruction)
            {
                var nextInstructionOffset =
                    Convert.ToInt32(bodyToProcess.Instructions[bodyToProcess.Instructions.IndexOf(instruction) + 1].Label.Substring(2), 16);
                var currentInstructionOffset = Convert.ToInt32(instruction.Label.Substring(2), 16);
                // short forms are 1 byte opcode + 1 byte target. normal forms are 1 byte opcode + 4 byte target
                var isShortForm = nextInstructionOffset - currentInstructionOffset == 2;
                return isShortForm;
            }
        }
    }
}
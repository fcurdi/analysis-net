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

namespace Backend.Transformations
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
            body.Parameters.AddRange(method.Body.Parameters); // FIXME esto queda igual?
            // this is updated later on. Needed to preserver variables that are declared but not used
            body.LocalVariables.AddRange(method.Body.LocalVariables);

            if (method.Body.Instructions.Count > 0)
            {
                var instructionTranslator = new InstructionTranslator(body, method.Body);
                instructionTranslator.Visit(method.Body);
                instructionTranslator.AssertNoProtectedBlocks();
            }

            body.UpdateVariables();
            var newLocals = body.LocalVariables.OfType<LocalVariable>().OrderBy(local => local.Index.Value).ToList();
            body.LocalVariables.Clear();
            body.LocalVariables.AddRange(newLocals);

            return body;
        }

        private class InstructionTranslator : InstructionVisitor
        {
            private readonly MethodBody body;
            private readonly MethodBody bodyToProcess;
            private uint offset;
            private readonly Stack<ProtectedBlockBuilder> protectedBlocks = new Stack<ProtectedBlockBuilder>();
            private readonly IDictionary<int, bool> ignoreInstruction = new Dictionary<int, bool>();

            public void AssertNoProtectedBlocks()
            {
                if (protectedBlocks.Count > 0) throw new Exception("Protected Blocks not generated correctly");
            }

            public InstructionTranslator(MethodBody body, MethodBody bodyToProcess)
            {
                this.body = body;
                this.bodyToProcess = bodyToProcess;
            }

            public override bool ShouldVisit(Instruction instruction)
            {
                var shouldProcessInstruction = !ignoreInstruction.TryGetValue(bodyToProcess.Instructions.IndexOf(instruction), out _);
                return shouldProcessInstruction;
            }

            public override void Visit(PopInstruction instruction)
            {
                body.Instructions.Add(new Bytecode.BasicInstruction(offset, Bytecode.BasicOperation.Pop));
                offset++;
            }

            public override void Visit(BinaryInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(offset, OperationHelper.ToBasicOperation(instruction.Operation))
                {
                    OverflowCheck = instruction.OverflowCheck,
                    UnsignedOperands = instruction.UnsignedOperands
                };
                body.Instructions.Add(basicInstruction);
                switch (basicInstruction.Operation)
                {
                    case Bytecode.BasicOperation.Gt:
                    case Bytecode.BasicOperation.Lt:
                    case Bytecode.BasicOperation.Eq:
                        offset += 2; // 2ByteOpcode
                        break;
                    default:
                        offset++;
                        break;
                }
            }

            public override void Visit(UnaryInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(offset, OperationHelper.ToBasicOperation(instruction.Operation));
                body.Instructions.Add(basicInstruction);
                offset++;
            }

            public override void Visit(LoadInstruction instruction)
            {
                // FIXME revisar los casos, hay algunos que no estoy seguro de que esten bien. se repiten caminos ademas (sobretodo por el reference)
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
                            offset += (uint) (loc.Index.Value > byte.MaxValue ? 4 : 2);
                        }
                        else
                        {
                            switch (loc.Index.Value)
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
                                    offset += (uint) (loc.Index.Value > byte.MaxValue ? 4 : 2);
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
                            case TemporalVariable _:
                            {
                                // FIXME hay casos sin sentido? a este no se entra nunca creo. Hay que revisar todo.
                                var operand = instruction.Result.ToLocalVariable(); // fixme operand como en l otro caso qie cambie?
                                bytecodeInstruction = new Bytecode.LoadInstruction(offset, Bytecode.LoadOperation.Content, operand);
                                switch (operand.Index.Value)
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
                                        offset += (uint) (operand.Index.Value > byte.MaxValue ? 4 : 2);
                                        break;
                                }

                                break;
                            }
                            case LocalVariable localVariable:
                            {
                                bytecodeInstruction = new Bytecode.LoadInstruction(offset, Bytecode.LoadOperation.Content, localVariable);
                                // fixme igual al caso de arriba? o estoy mezclando los casos? Creo que esta bien porque local y temporal ambas pueden
                                // fixme ser parameter (esto determina si es ldloc o ldarg)
                                switch (localVariable.Index.Value)
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
                                        offset += (uint) (localVariable.Index.Value > byte.MaxValue ? 4 : 2);
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
                                        offset += (uint) (localVariable.Index.Value > byte.MaxValue ? 4 : 2);
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

                                if ((type.ElementsType.IsIntType() ||
                                     type.ElementsType.IsFloatType() ||
                                     type.ElementsType.Equals(PlatformTypes.IntPtr) ||
                                     type.ElementsType.Equals(PlatformTypes.Object)) && ((ArrayType) arrayElementAccess.Array.Type).IsVector)
                                    /// fixme cuaqluiera ese isvector?
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

                body.Instructions.Add(bytecodeInstruction);
            }

            public override void Visit(StoreInstruction instruction)
            {
                Bytecode.Instruction storeInstruction;
                switch (instruction.Result)
                {
                    case ArrayElementAccess arrayElementAccess:
                    {
                        var type = (ArrayType) arrayElementAccess.Array.Type;
                        storeInstruction = new Bytecode.StoreArrayElementInstruction(offset, type) {Method = arrayElementAccess.Method};
                        if (
                            (type.ElementsType.IsIntType() ||
                             type.ElementsType.IsFloatType() ||
                             type.ElementsType.Equals(PlatformTypes.IntPtr) ||
                             type.ElementsType.Equals(PlatformTypes.Object)
                            ) && ((ArrayType) arrayElementAccess.Array.Type).IsVector /// fixme cuaqluiera ese isvector?
                        )
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

                body.Instructions.Add(storeInstruction);
            }

            public override void Visit(NopInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(offset, Bytecode.BasicOperation.Nop);
                body.Instructions.Add(basicInstruction);
                offset++;
            }

            public override void Visit(BreakpointInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(offset, Bytecode.BasicOperation.Breakpoint);
                body.Instructions.Add(basicInstruction);
                offset++;
            }

            public override void Visit(TryInstruction instruction)
            {
                // try with multiple handlers are modelled as multiple try instructions with the same label but different handlers.
                // if label matches with the current try, then increase the number of expected handlers
                if (protectedBlocks.Count > 0 && protectedBlocks.Peek().TryStart.Equals(offset))
                {
                    protectedBlocks.Peek().HandlerCount++;
                }
                else
                {
                    var exceptionBlockBuilder = new ProtectedBlockBuilder {TryStart = offset, HandlerCount = 1};
                    protectedBlocks.Push(exceptionBlockBuilder);
                }
            }

            public override void Visit(FaultInstruction instruction)
            {
                protectedBlocks
                    .Peek()
                    .EndPreviousRegion(offset)
                    .Handlers.Add(new ExceptionHandlerBlockBuilder
                    {
                        HandlerStart = offset,
                        HandlerBlockKind = ExceptionHandlerBlockKind.Fault,
                    });
            }

            public override void Visit(FinallyInstruction instruction)
            {
                protectedBlocks
                    .Peek()
                    .EndPreviousRegion(offset)
                    .Handlers.Add(
                        new ExceptionHandlerBlockBuilder
                        {
                            HandlerStart = offset,
                            HandlerBlockKind = ExceptionHandlerBlockKind.Finally,
                        });
            }

            public override void Visit(FilterInstruction instruction)
            {
                var protectedBlockBuilder = protectedBlocks.Peek();

                // filter is a special case since it has a two regions (filter and handler). A filter in this TAC is moddeled as two FilterInstruction
                // with different kinds. If the previous region is a Filter, it must be ended only if it is in it's handler part.
                bool EndPreviousHandlerCondition() => protectedBlockBuilder.Handlers.Last().HandlerBlockKind != ExceptionHandlerBlockKind.Filter ||
                                                      instruction.kind == FilterInstructionKind.FilterSection;

                protectedBlockBuilder.EndPreviousRegion(offset, EndPreviousHandlerCondition);

                switch (instruction.kind)
                {
                    case FilterInstructionKind.FilterSection:
                        protectedBlockBuilder.Handlers.Add(
                            new ExceptionHandlerBlockBuilder
                            {
                                FilterStart = offset,
                                HandlerBlockKind = ExceptionHandlerBlockKind.Filter,
                            });
                        break;
                    case FilterInstructionKind.FilterHandler:
                        var handler = protectedBlockBuilder.Handlers.Last();
                        handler.HandlerStart = offset;
                        handler.ExceptionType = instruction.ExceptionType;
                        break;
                    default: throw instruction.kind.ToUnknownValueException();
                }
            }

            public override void Visit(CatchInstruction instruction)
            {
                protectedBlocks
                    .Peek()
                    .EndPreviousRegion(offset)
                    .Handlers
                    .Add(
                        new ExceptionHandlerBlockBuilder()
                        {
                            HandlerStart = offset,
                            HandlerBlockKind = ExceptionHandlerBlockKind.Catch,
                            ExceptionType = instruction.ExceptionType
                        }
                    );
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
                body.Instructions.Add(convertInstruction);
                switch (convertInstruction.Operation)
                {
                    case Bytecode.ConvertOperation.Conv:
                        offset++;
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
                body.Instructions.Add(basicInstruction);
                offset++;
            }

            public override void Visit(ThrowInstruction instruction)
            {
                Bytecode.BasicInstruction basicInstruction;
                if (instruction.HasOperand)
                {
                    basicInstruction = new Bytecode.BasicInstruction(offset, Bytecode.BasicOperation.Throw);
                    offset++;
                }
                else
                {
                    basicInstruction = new Bytecode.BasicInstruction(offset, Bytecode.BasicOperation.Rethrow);
                    offset += 2; // 2byte opcode
                }

                EndProtectedBlockIfApplies();
                body.Instructions.Add(basicInstruction);
            }

            public override void Visit(UnconditionalBranchInstruction instruction)
            {
                switch (instruction.Operation)
                {
                    case UnconditionalBranchOperation.Leave:
                    {
                        var target = Convert.ToUInt32(instruction.Target.Substring(2), 16);
                        var unconditionalBranchInstruction = new Bytecode.BranchInstruction(offset, Bytecode.BranchOperation.Leave, target);

                        body.Instructions.Add(unconditionalBranchInstruction);
                        // leave target -> DD <int32> (1 + 4)
                        // leave.s target -> DE <int8> (1 + 1)
                        offset += (uint) (IsShortForm(instruction) ? 2 : 5);
                        EndProtectedBlockIfApplies();
                        break;
                    }
                    case UnconditionalBranchOperation.Branch:
                    {
                        var target = Convert.ToUInt32(instruction.Target.Substring(2), 16);
                        var unconditionalBranchInstruction = new Bytecode.BranchInstruction(
                            offset,
                            Bytecode.BranchOperation.Branch,
                            target);
                        body.Instructions.Add(unconditionalBranchInstruction);
                        // leave target -> 38 <int32> (1 + 4)
                        // leave.s target -> 2B <int8> (1 + 1)
                        offset += (uint) (IsShortForm(instruction) ? 2 : 5);
                        break;
                    }
                    case UnconditionalBranchOperation.EndFinally:
                    {
                        body.Instructions.Add(new Bytecode.BasicInstruction(offset, Bytecode.BasicOperation.EndFinally));
                        offset++;
                        var exceptionBlockBuilder = protectedBlocks.Pop(); // no more handlers after finally
                        exceptionBlockBuilder
                            .Handlers
                            .Last()
                            .HandlerEnd = offset;
                        body.ExceptionInformation.AddRange(exceptionBlockBuilder.Build());
                        break;
                    }
                    case UnconditionalBranchOperation.EndFilter:
                    {
                        // nothing is done with protectedBlocks since filter area is the gap between try end and handler start
                        body.Instructions.Add(new Bytecode.BasicInstruction(offset, Bytecode.BasicOperation.EndFilter));
                        offset += 2; // 2byte opcode
                        break;
                    }
                    default: throw instruction.Operation.ToUnknownValueException();
                }
            }

            public override void Visit(ConditionalBranchInstruction instruction)
            {
                var target = Convert.ToUInt32(instruction.Target.Substring(2), 16);
                var conditionalBranchInstruction = new Bytecode.BranchInstruction(
                    offset,
                    OperationHelper.ToBranchOperation(instruction.Operation, instruction.RightOperand),
                    target);
                body.Instructions.Add(conditionalBranchInstruction);
                // br* target -> 1ByteOpcode <int32> (1 + 4)
                // br*.s target -> 1ByteOpcode <int8> (1 + 1)
                offset += (uint) (IsShortForm(instruction) ? 2 : 5);
            }

            public override void Visit(SwitchInstruction instruction)
            {
                var targets = instruction.Targets.Select(target => Convert.ToUInt32(target.Substring(2), 16)).ToList();
                var switchInstruction = new Bytecode.SwitchInstruction(offset, targets);
                body.Instructions.Add(switchInstruction);
                // switch number t1 t2 t3 ... -> 45 <uint32> <int32> <int32> <int32> .... (1 + 4 + n*4)
                offset += (uint) (1 + 4 + 4 * targets.Count);
            }

            public override void Visit(SizeofInstruction instruction)
            {
                var sizeofInstruction = new Bytecode.SizeofInstruction(offset, instruction.MeasuredType);
                body.Instructions.Add(sizeofInstruction);
                // sizeof typetok -> FE 1C <Token> (2 + 4)
                offset += 6;
            }

            public override void Visit(LoadTokenInstruction instruction)
            {
                var loadTokenInstruction = new Bytecode.LoadTokenInstruction(offset, instruction.Token);
                body.Instructions.Add(loadTokenInstruction);
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
                body.Instructions.Add(methodCallInstruction);
                // call method -> 28 <Token> (1 + 4)
                // callvirt method -> 6F <Token> (1 + 4)
                offset += 5;
            }

            public override void Visit(IndirectMethodCallInstruction instruction)
            {
                var indirectMethodCallInstruction = new Bytecode.IndirectMethodCallInstruction(offset, instruction.Function);
                body.Instructions.Add(indirectMethodCallInstruction);
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
                body.Instructions.Add(createObjectInstruction);
                // newobj ctor -> 73 <Token> (1 + 4)
                offset += 5;
            }

            public override void Visit(CopyMemoryInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(offset, Bytecode.BasicOperation.CopyBlock);
                body.Instructions.Add(basicInstruction);
                offset += 2; // 2ByteOpcode
            }

            public override void Visit(LocalAllocationInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(offset, Bytecode.BasicOperation.LocalAllocation);
                body.Instructions.Add(basicInstruction);
                offset += 2; // 2ByteOpcode
            }

            public override void Visit(InitializeMemoryInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(offset, Bytecode.BasicOperation.InitBlock);
                body.Instructions.Add(basicInstruction);
                offset += 2; // 2ByteOpcode
            }

            public override void Visit(InitializeObjectInstruction instruction)
            {
                var initObjInstruction = new Bytecode.InitObjInstruction(offset, instruction.TargetAddress.Type);
                body.Instructions.Add(initObjInstruction);
                // initobj typeTok -> FE 15 <Token> (2 + 4)
                offset += 6;
            }

            public override void Visit(CopyObjectInstruction instruction)
            {
                var basicInstruction = new Bytecode.BasicInstruction(offset, Bytecode.BasicOperation.CopyObject);
                body.Instructions.Add(basicInstruction);
                // cpobj typeTok-> 70 <Token> (1 + 4)
                offset += 5;
            }

            public override void Visit(CreateArrayInstruction instruction)
            {
                var createArrayInstruction =
                    new Bytecode.CreateArrayInstruction(offset, new ArrayType(instruction.ElementType, instruction.Rank))
                    {
                        WithLowerBound = instruction.LowerBounds.Any(),
                        Constructor = instruction.Constructor
                    };
                body.Instructions.Add(createArrayInstruction);
                // newobj ctor -> 73 <Token> (1 + 4)
                // newarr etype -> 8D <Token> (1 + 4)
                offset += 5;
            }

            public override void Visit(PhiInstruction instruction)
            {
                throw new Exception();
            }

            // FIXME ahora que calculo a mano los offsets, se puede sacar esta instruccion y ver bien cuando ponerla en el virtual call?
            public override void Visit(ConstrainedInstruction instruction)
            {
                body.Instructions.Add(new Bytecode.ConstrainedInstruction(offset, instruction.ThisType));
                // constrained. thisType -> FE 16 <T> (2 + 4)
                offset += 6;
            }


            private bool IsShortForm(BranchInstruction instruction)
            {
                var nextInstructionOffset =
                    Convert.ToInt32(bodyToProcess.Instructions[bodyToProcess.Instructions.IndexOf(instruction) + 1].Label.Substring(2), 16);
                var currentInstructionOffset = Convert.ToInt32(instruction.Label.Substring(2), 16);
                // short forms are 1 byte opcode + 1 byte target. normal forms are 1 byte opcode + 4 byte target
                var isShortForm = nextInstructionOffset - currentInstructionOffset == 2;
                return isShortForm;
            }

            private void EndProtectedBlockIfApplies()
            {
                if (protectedBlocks.Count > 0 && protectedBlocks.Peek().AllHandlersAdded())
                {
                    var exceptionBlockBuilder = protectedBlocks.Pop();
                    exceptionBlockBuilder
                        .Handlers
                        .Last()
                        .HandlerEnd = offset;
                    body.ExceptionInformation.AddRange(exceptionBlockBuilder.Build());
                }
            }


            #region ExceptionInformation

            private class ProtectedBlockBuilder
            {
                private uint? tryStart;

                public uint TryStart
                {
                    get => tryStart ?? throw new Exception("TryStart was not set");
                    set
                    {
                        if (tryStart != null) throw new Exception("TryStart was already set");
                        tryStart = value;
                    }
                }

                private uint? tryEnd;

                private uint TryEnd
                {
                    get => tryEnd ?? throw new Exception("TryEnd was not set");
                    set
                    {
                        if (tryEnd != null) throw new Exception("TryEnd was already set");
                        tryEnd = value;
                    }
                }

                public uint HandlerCount { get; set; }

                public bool AllHandlersAdded() => HandlerCount == Handlers.Count;

                public readonly IList<ExceptionHandlerBlockBuilder> Handlers = new List<ExceptionHandlerBlockBuilder>();

                public ProtectedBlockBuilder EndPreviousRegion(uint offset) => EndPreviousRegion(offset, () => true);

                public ProtectedBlockBuilder EndPreviousRegion(uint offset, Func<bool> multipleHandlerCondition)
                {
                    if (Handlers.Count == 0) // first handler, ends try region
                    {
                        TryEnd = offset;
                    }
                    else if (multipleHandlerCondition()) // multiple handlers. End previous handler conditionally
                    {
                        Handlers.Last().HandlerEnd = offset;
                    }

                    return this;
                }

                public IList<ProtectedBlock> Build() =>
                    Handlers
                        .Select(handlerBuilder => handlerBuilder.Build())
                        .Select(handler => new ProtectedBlock(TryStart, TryEnd) {Handler = handler})
                        .ToList();
            }

            private class ExceptionHandlerBlockBuilder
            {
                private uint? filterStart;

                public uint FilterStart
                {
                    get => filterStart ?? throw new Exception("FilterStart was not set");
                    set
                    {
                        if (filterStart != null) throw new Exception("FilterStart was already set");
                        filterStart = value;
                    }
                }

                private uint? handlerStart;

                public uint HandlerStart
                {
                    get => handlerStart ?? throw new Exception("HandlerStart was not set");
                    set
                    {
                        if (handlerStart != null) throw new Exception("HandlerStart was already set");
                        handlerStart = value;
                    }
                }

                private uint? handlerEnd;

                public uint HandlerEnd
                {
                    get => handlerEnd ?? throw new Exception("HandlerEnd was not set");
                    set
                    {
                        if (handlerEnd != null) throw new Exception("HandlerEnd was already set");
                        handlerEnd = value;
                    }
                }

                private ExceptionHandlerBlockKind? handlerBlockKind;

                public ExceptionHandlerBlockKind HandlerBlockKind
                {
                    get => handlerBlockKind ?? throw new Exception("HandlerBlockKind was not set");
                    set
                    {
                        if (handlerBlockKind != null) throw new Exception("HandlerBlockKind was already set");
                        handlerBlockKind = value;
                    }
                }

                private IType exceptionType;

                public IType ExceptionType
                {
                    get => exceptionType ?? throw new Exception("ExceptionType was not set");
                    set
                    {
                        if (exceptionType != null) throw new Exception("ExceptionType was already set");
                        exceptionType = value;
                    }
                }

                public IExceptionHandler Build()
                {
                    switch (HandlerBlockKind)
                    {
                        case ExceptionHandlerBlockKind.Filter:
                            return new FilterExceptionHandler(FilterStart, HandlerStart, HandlerEnd, ExceptionType);
                        case ExceptionHandlerBlockKind.Catch:
                            return new CatchExceptionHandler(HandlerStart, HandlerEnd, ExceptionType);
                        case ExceptionHandlerBlockKind.Fault:
                            return new FaultExceptionHandler(HandlerStart, HandlerEnd);
                        case ExceptionHandlerBlockKind.Finally:
                            return new FinallyExceptionHandler(HandlerStart, HandlerEnd);
                        default: throw new UnknownValueException<ExceptionHandlerBlockKind>(HandlerBlockKind);
                    }
                }
            }

            #endregion
        }
    }
}
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
            body.Parameters.AddRange(method.Body.Parameters);
            body.LocalVariables.AddRange(method.Body.LocalVariables.Select(variable => variable.ToLocalVariable()));

            if (method.Body.Instructions.Count > 0)
            {
                new InstructionTranslator(body).Visit(method.Body);
            }

            return body;
        }

        private class InstructionTranslator : InstructionVisitor
        {
            private readonly MethodBody body;
            private uint offset;
            private readonly Stack<ProtectedBlockBuilder> protectedBlocks = new Stack<ProtectedBlockBuilder>();

            public InstructionTranslator(MethodBody body)
            {
                this.body = body;
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
                Bytecode.Instruction loadInstruction;
                if (instruction.Operand is TemporalVariable && instruction.Result is TemporalVariable)
                {
                    if (instruction.Operand.Equals(instruction.Result))
                    {
                        return;
                    }
                    else
                    {
                        loadInstruction = new Bytecode.BasicInstruction(offset, Bytecode.BasicOperation.Dup);
                        offset++;
                    }
                }
                else
                {
                    switch (instruction.Operand)
                    {
                        case Constant constant:
                            loadInstruction = new Bytecode.LoadInstruction(offset, Bytecode.LoadOperation.Value, constant);
                            if (constant.Value == null)
                            {
                                // ldnull -> 14 (1) 
                                offset++;
                            }
                            else if (constant.Type.Equals(PlatformTypes.String))
                            {
                                // ldstr string -> 72 <T> (1 + 4)  
                                offset += 5;
                            }
                            else
                            {
                                switch (constant.Value)
                                {
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
                                    default:
                                        if (constant.Type.IsOneOf(PlatformTypes.Int8, PlatformTypes.UInt8))
                                        {
                                            // ldc.i4.s num -> 1F <int8> (1 + 1)
                                            offset += 2;
                                        }
                                        else if (constant.Type.IsOneOf(PlatformTypes.Int16, PlatformTypes.Int32, PlatformTypes.UInt16,
                                            PlatformTypes.UInt32))
                                        {
                                            // ldc.i4 num -> 20 <int32> (1 + 4)
                                            offset += 5;
                                        }
                                        else if (constant.Type.IsOneOf(PlatformTypes.Int64, PlatformTypes.UInt64))
                                        {
                                            // ldc.i8 num-> 21 <int64> (1 + 8)
                                            offset += 9;
                                        }
                                        else if (constant.Type.Equals(PlatformTypes.Float32))
                                        {
                                            // ldc.r4 num -> 22 <float32> (1 + 4) 
                                            offset += 5;
                                        }
                                        else if (constant.Type.Equals(PlatformTypes.Float64))
                                        {
                                            // ldc.r8 num -> 23 <float64> (1 + 8)
                                            offset += 9;
                                        }

                                        break;
                                }
                            }


                            break;
                        case TemporalVariable _:
                        {
                            var operand = instruction.Result.ToLocalVariable();
                            loadInstruction = new Bytecode.LoadInstruction(offset, Bytecode.LoadOperation.Content, operand);
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
                            loadInstruction = new Bytecode.LoadInstruction(offset, Bytecode.LoadOperation.Content, localVariable);
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
                            loadInstruction = new Bytecode.LoadIndirectInstruction(offset, type);
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
                                case ArrayElementAccess _:
                                {
                                    var operand = instruction.Result.ToLocalVariable();
                                    loadInstruction = new Bytecode.LoadInstruction(offset, Bytecode.LoadOperation.Address, operand);
                                    // ldelema typeTok -> 8F <Token> (1 + 4) 
                                    offset += 5;
                                    break;
                                }

                                case LocalVariable _:
                                {
                                    var operand = instruction.Result.ToLocalVariable();
                                    loadInstruction = new Bytecode.LoadInstruction(offset, Bytecode.LoadOperation.Address, operand);
                                    // ldloca indx -> FE 0D <unsigned int16>  (2 + 2) 
                                    // ldloca.s indx -> 12 <unsigned int8> (1 + 1)
                                    // ldarga argNum -> FE 0A <unsigned int16> (2 + 2)
                                    // ldarga.s argNum -> 0F <unsigned int8> (1 + 1)
                                    offset += (uint) (operand.Index.Value > byte.MaxValue ? 4 : 2);
                                    break;
                                }
                                case InstanceFieldAccess instanceFieldAccess:
                                    loadInstruction = new Bytecode.LoadFieldInstruction(
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
                            loadInstruction = new Bytecode.BasicInstruction(offset, Bytecode.BasicOperation.LoadArrayLength);
                            offset++;
                            break;
                        case VirtualMethodReference virtualMethodReference:
                            loadInstruction = new Bytecode.LoadMethodAddressInstruction(
                                offset,
                                Bytecode.LoadMethodAddressOperation.Virtual,
                                virtualMethodReference.Method);
                            // ldvirtftn method -> FE 07 <Token> (2 + 4)  
                            offset += 6;
                            break;
                        case StaticMethodReference staticMethodReference:
                            loadInstruction = new Bytecode.LoadMethodAddressInstruction(
                                offset,
                                Bytecode.LoadMethodAddressOperation.Static,
                                staticMethodReference.Method);
                            // ldftn method -> FE 06 <Token> (2 + 4)  
                            offset += 6;
                            break;
                        case InstanceFieldAccess instanceFieldAccess:
                            loadInstruction = new Bytecode.LoadFieldInstruction(
                                offset,
                                Bytecode.LoadFieldOperation.Content,
                                instanceFieldAccess.Field);
                            //  ldfld field -> 7B <T> (1 + 4)
                            offset += 5;
                            break;
                        case StaticFieldAccess staticFieldAccess:
                            loadInstruction = new Bytecode.LoadFieldInstruction(
                                offset,
                                Bytecode.LoadFieldOperation.Content,
                                staticFieldAccess.Field);
                            // ldsfld field -> 7E <T> (1 + 4)
                            offset += 5;
                            break;
                        case ArrayElementAccess arrayElementAccess:
                        {
                            var type = (ArrayType) arrayElementAccess.Array.Type;
                            loadInstruction = new Bytecode.LoadArrayElementInstruction(
                                offset,
                                Bytecode.LoadArrayElementOperation.Content,
                                type);

                            if (type.ElementsType.IsIntType() ||
                                type.ElementsType.IsFloatType() ||
                                type.ElementsType.Equals(PlatformTypes.IntPtr) ||
                                type.ElementsType.Equals(PlatformTypes.Object))
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

                body.Instructions.Add(loadInstruction);
            }

            public override void Visit(StoreInstruction instruction)
            {
                Bytecode.Instruction storeInstruction;
                switch (instruction.Result)
                {
                    case ArrayElementAccess arrayElementAccess:
                    {
                        var type = (ArrayType) arrayElementAccess.Array.Type;
                        storeInstruction = new Bytecode.StoreArrayElementInstruction(offset, type);
                        if (type.ElementsType.IsIntType() ||
                            type.ElementsType.IsFloatType() ||
                            type.ElementsType.Equals(PlatformTypes.IntPtr) ||
                            type.ElementsType.Equals(PlatformTypes.Object))
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
                // FIXME rethrow se modela como ThrowInstruction, hay que generarla. Habria que ver si esta dentro de un catch creo para saber si es o no.
                // FIXME admeas el offset cambia si es rethrow.
                var basicInstruction = new Bytecode.BasicInstruction(offset, Bytecode.BasicOperation.Throw);
                body.Instructions.Add(basicInstruction);
                offset++;
            }

            public override void Visit(UnconditionalBranchInstruction instruction)
            {
                switch (instruction.Operation)
                {
                    case UnconditionalBranchOperation.Leave:
                    {
                        if (protectedBlocks.Count > 0)
                        {
                            if (protectedBlocks.Peek().AllHandlersAdded())
                            {
                                var exceptionBlockBuilder = protectedBlocks.Pop();
                                exceptionBlockBuilder
                                    .Handlers
                                    .Last()
                                    .HandlerEnd = offset;
                                body.ExceptionInformation.AddRange(exceptionBlockBuilder.Build());
                            }
                        }

                        var target = Convert.ToUInt32(instruction.Target.Substring(2), 16);
                        var unconditionalBranchInstruction = new Bytecode.BranchInstruction(offset, Bytecode.BranchOperation.Leave, target);
                        body.Instructions.Add(unconditionalBranchInstruction);
                        // leave target -> DD <int32> (1 + 4)
                        // leave.s target -> DE <int8> (1 + 1)
                        offset += (uint) (target > byte.MaxValue ? 5 : 2);
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
                        offset += (uint) (target > byte.MaxValue ? 5 : 2);
                        break;
                    }
                    case UnconditionalBranchOperation.EndFinally:
                    {
                        var exceptionBlockBuilder = protectedBlocks.Pop(); // no more handlers after finally
                        exceptionBlockBuilder
                            .Handlers
                            .Last()
                            .HandlerEnd = offset;
                        body.ExceptionInformation.AddRange(exceptionBlockBuilder.Build());
                        body.Instructions.Add(new Bytecode.BasicInstruction(offset, Bytecode.BasicOperation.EndFinally));
                        offset++;
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
                offset += (uint) (target > byte.MaxValue ? 5 : 2);
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
                var createObjectInstruction = new Bytecode.CreateObjectInstruction(offset, instruction.Constructor);
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
                        WithLowerBound = instruction.LowerBounds.Any()
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
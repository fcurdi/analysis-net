using System;
using Model;
using Model.Bytecode;
using Model.ThreeAddressCode.Values;
using Model.Types;

namespace Backend.Transformations.Assembly
{
    public static class CilInstructionSizeCalculator
    {
        public static uint SizeOf(Instruction instruction, uint nexInstructionOffset)
        {
            switch (instruction)
            {
                case BasicInstruction basicInstruction: return SizeOf(basicInstruction);
                case ConstrainedInstruction constrainedInstruction: return SizeOf(constrainedInstruction);
                case CreateArrayInstruction createArrayInstruction: return SizeOf(createArrayInstruction);
                case InitObjInstruction initObjInstruction: return SizeOf(initObjInstruction);
                case CreateObjectInstruction createObjectInstruction: return SizeOf(createObjectInstruction);
                case IndirectMethodCallInstruction indirectMethodCallInstruction: return SizeOf(indirectMethodCallInstruction);
                case MethodCallInstruction methodCallInstruction: return SizeOf(methodCallInstruction);
                case LoadTokenInstruction loadTokenInstruction: return SizeOf(loadTokenInstruction);
                case SizeofInstruction sizeofInstruction: return SizeOf(sizeofInstruction);
                case SwitchInstruction switchInstruction: return SizeOf(switchInstruction);
                case ConvertInstruction convertInstruction: return SizeOf(convertInstruction);
                case StoreIndirectInstruction storeIndirectInstruction: return SizeOf(storeIndirectInstruction);
                case StoreFieldInstruction storeFieldInstruction: return SizeOf(storeFieldInstruction);
                case StoreArrayElementInstruction storeArrayElementInstruction: return SizeOf(storeArrayElementInstruction);
                case BranchInstruction branchInstruction: return SizeOf(branchInstruction, nexInstructionOffset);
                case StoreInstruction storeInstruction: return SizeOf(storeInstruction);
                case LoadInstruction loadInstruction: return SizeOf(loadInstruction);
                case LoadIndirectInstruction loadIndirectInstruction: return SizeOf(loadIndirectInstruction);
                case LoadArrayElementInstruction loadArrayElementInstruction: return SizeOf(loadArrayElementInstruction);
                case LoadFieldInstruction loadFieldInstruction: return SizeOf(loadFieldInstruction);
                case LoadMethodAddressInstruction loadMethodAddressInstruction: return SizeOf(loadMethodAddressInstruction);
                default: throw new Exception(); // TODO
            }
        }

        private static uint SizeOf(LoadMethodAddressInstruction loadMethodAddressInstruction)
        {
            // ldvirtftn method -> FE 07 <Token> (2 + 4)
            // ldftn method -> FE 06 <Token> (2 + 4)  
            switch (loadMethodAddressInstruction.Operation)
            {
                case LoadMethodAddressOperation.Static:
                case LoadMethodAddressOperation.Virtual:
                    return 6;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static uint SizeOf(LoadFieldInstruction loadFieldInstruction)
        {
            //  ldfld field -> 7B <T> (1 + 4)
            // ldsfld field -> 7E <T> (1 + 4)
            // ldflda field -> 0x7C <Token>  (1 + 4)
            // ldsflda field -> 0x7F <Token> (1 + 4)
            switch (loadFieldInstruction.Operation)
            {
                case LoadFieldOperation.Content:
                case LoadFieldOperation.Address: return 5;
                default: throw new ArgumentOutOfRangeException();
            }
        }

        private static uint SizeOf(LoadArrayElementInstruction loadArrayElementInstruction)
        {
            switch (loadArrayElementInstruction.Operation)
            {
                case LoadArrayElementOperation.Content:
                    var type = loadArrayElementInstruction.Array;
                    if (type.IsVector &&
                        (type.ElementsType.IsIntType() ||
                         type.ElementsType.IsFloatType() ||
                         type.ElementsType.Equals(PlatformTypes.IntPtr) ||
                         type.ElementsType.Equals(PlatformTypes.Object)))
                    {
                        // 1 byte opcode
                        return 1;
                    }
                    else
                    {
                        // ldelem typeTok -> A3 <Token> (1 + 4) 
                        return 5;
                    }
                case LoadArrayElementOperation.Address:
                    // ldelema typeTok -> 8F <Token> (1 + 4) 
                    // ldobj typeTok -> 71 <Token> (1 + 4)
                    return 5;
                default:
                    throw new ArgumentOutOfRangeException();
                // FIXME quiza puedo usar esta que es la defualt para estos casos siempre para el caso default en vez de hacer una yo
            }
        }

        private static uint SizeOf(LoadIndirectInstruction loadIndirectInstruction)
        {
            var type = loadIndirectInstruction.Type;
            return (uint) (type.IsIntType() || type.IsFloatType() || type.Equals(PlatformTypes.IntPtr) || type.Equals(PlatformTypes.Object)
                ? 1 // 1 byte opcode
                : 5); // ldobj typeTok -> 71 <Token> (1 + 4)
        }

        private static uint SizeOf(LoadInstruction loadInstruction)
        {
            switch (loadInstruction.Operation)
            {
                case LoadOperation.Value:
                    var constant = (Constant) loadInstruction.Operand;
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
                        case null:
                            return 1; // ldnull -> 14 (1)
                        case string _:
                            return 5; // ldstr string -> 72 <T> (1 + 4)  
                        case object _ when constant.Type.IsOneOf(PlatformTypes.Int8, PlatformTypes.Int16, PlatformTypes.Int32):
                        {
                            // ldc.i4.s num -> 1F <int8> (1 + 1)
                            // ldc.i4 num -> 20 <int32> (1 + 4)
                            var value = (int) constant.Value;
                            return (uint) (value >= sbyte.MinValue && value <= sbyte.MaxValue ? 2 : 5);
                        }
                        case object _ when constant.Type.Equals(PlatformTypes.Int64): return 9; // ldc.i8 num-> 21 <int64> (1 + 8)
                        case object _ when constant.Type.IsOneOf(PlatformTypes.UInt8, PlatformTypes.UInt16, PlatformTypes.UInt32):
                        {
                            // ldc.i4.s num -> 1F <int8> (1 + 1)
                            // ldc.i4 num -> 20 <int32> (1 + 4)
                            var value = (uint) constant.Value;
                            return (uint) (value <= byte.MaxValue ? 2 : 5);
                        }
                        case object _ when constant.Type.Equals(PlatformTypes.UInt64): return 9; // ldc.i8 num-> 21 <int64> (1 + 8)
                        case object _ when constant.Type.Equals(PlatformTypes.Float32): return 5; // ldc.r4 num -> 22 <float32> (1 + 4)
                        case object _ when constant.Type.Equals(PlatformTypes.Float64): return 9; // ldc.r8 num -> 23 <float64> (1 + 8)
                        default: throw new Exception();
                    }

                case LoadOperation.Content:
                {
                    var localVariable = (LocalVariable) loadInstruction.Operand;
                    switch (localVariable.Index)
                    {
                        // ldloc.0,1,2,3 ldarg.0,1,2,3
                        case 0:
                        case 1:
                        case 2:
                        case 3:
                            return 1;
                        default:
                            // ldloc indx -> FE 0C <unsigned int16>  (2 + 2) 
                            // ldloc.s indx -> 11 <unsigned int8> (1 + 1) 
                            // ldarg num -> FE 09 <unsigned int16> (2 + 2) 
                            // ldarg.s num -> 0E <unsigned int8>  (1 + 1)
                            return (uint) (localVariable.Index > byte.MaxValue ? 4 : 2);
                    }
                }
                case LoadOperation.Address:
                {
                    // ldloca indx -> FE 0D <unsigned int16>  (2 + 2) 
                    // ldloca.s indx -> 12 <unsigned int8> (1 + 1)
                    // ldarga argNum -> FE 0A <unsigned int16> (2 + 2)
                    // ldarga.s argNum -> 0F <unsigned int8> (1 + 1)
                    var localVariable = (LocalVariable) loadInstruction.Operand;
                    return (uint) (localVariable.Index > byte.MaxValue ? 4 : 2);
                }
                default: throw new Exception(); // TODO
            }
        }

        private static uint SizeOf(StoreInstruction storeInstruction)
        {
            var local = (LocalVariable) storeInstruction.Target;
            if (local.IsParameter)
            {
                // starg num -> FE 0B <unsigned int16> (2 + 2) 
                // starg.s num -> 10 <unsigned int8> (1 + 1)
                return (uint) (local.Index > byte.MaxValue ? 4 : 2);
            }
            else
            {
                switch (local.Index)
                {
                    case 0:
                    case 1:
                    case 2:
                    case 3:
                        return 1; // 1byte OpCode
                    default:
                        // stloc indx -> FE 0E <unsigned int16> (2 + 2) 
                        // stloc.s indx -> 13 <unsigned int8> (1 + 1) 
                        return (uint) (local.Index > byte.MaxValue ? 4 : 2);
                }
            }
        }
        // leave target -> DD <int32> (1 + 4)
        // leave.s target -> DE <int8> (1 + 1)
        // br target -> 38 <int32> (1 + 4) 
        // br.s target -> 2B <int8> (1 + 1)
        // br* target -> 1ByteOpcode <int32> (1 + 4)
        // br*.s target -> 1ByteOpcode <int8> (1 + 1)

        private static uint SizeOf(this BranchInstruction branchInstruction, uint nextInstructionOffset)
        {
            // short forms are 1 byte opcode + 1 byte target. normal forms are 1 byte opcode + 4 byte target
            var isShortForm = nextInstructionOffset - branchInstruction.Offset == 2;
            return (uint) (isShortForm ? 2 : 5);
        }

        private static uint SizeOf(StoreArrayElementInstruction storeArrayElementInstruction)
        {
            var type = storeArrayElementInstruction.Array;
            if (type.IsVector &&
                (type.ElementsType.IsIntType() ||
                 type.ElementsType.IsFloatType() ||
                 type.ElementsType.Equals(PlatformTypes.IntPtr) ||
                 type.ElementsType.Equals(PlatformTypes.Object)))
            {
                // 1 byte opcode
                return 1;
            }
            else
            {
                // stelem typeTok -> A4 <Token> (1 + 4) 
                return 5;
            }
        }

        // stfld field -> 7D <T> (1 + 4)
        // stsfld field -> 80 <T> (1 + 4)
        private static uint SizeOf(StoreFieldInstruction storeFieldInstruction) => 5;

        private static uint SizeOf(StoreIndirectInstruction storeIndirectInstruction)
        {
            var type = storeIndirectInstruction.Type;
            if (type.IsIntType() || type.IsFloatType() || type.Equals(PlatformTypes.IntPtr) || type.Equals(PlatformTypes.Object))
            {
                // 1 byte opcode
                return 1;
            }
            else
            {
                // stobj typeTok -> 81 <Token> (1 + 4) 
                return 5;
            }
        }

        private static uint SizeOf(ConvertInstruction convertInstruction)
        {
            switch (convertInstruction.Operation)
            {
                case ConvertOperation.Conv:
                    return 1; // 1 Byte OpCode
                case ConvertOperation.IsInst:
                case ConvertOperation.Cast:
                case ConvertOperation.Box:
                case ConvertOperation.Unbox:
                case ConvertOperation.UnboxPtr:
                    // box typeTok -> 8C <Token> (1 + 4) 
                    // castclass typeTok -> 74 <Token> (1 + 4) 
                    // isinst typeTok -> 75 <Token> (1 + 4) 
                    // unbox valuetype -> 79 <Token> (1 + 4) 
                    // unbox.any typeTok -> A5 <Token> (1 + 4) 
                    return 5;
                default: throw new Exception();
            }
        }

        // switch number t1 t2 t3 ... -> 45 <uint32> <int32> <int32> <int32> .... (1 + 4 + n*4)
        private static uint SizeOf(SwitchInstruction switchInstruction) => (uint) (1 + 4 + 4 * switchInstruction.Targets.Count);

        // sizeof typetok -> FE 1C <Token> (2 + 4)
        private static uint SizeOf(SizeofInstruction sizeofInstruction) => 6;

        private static uint SizeOf(LoadTokenInstruction loadTokenInstruction) => 5; //  ldtoken token -> D0 <Token> (1 + 4)

        // call method -> 28 <Token> (1 + 4)
        // callvirt method -> 6F <Token> (1 + 4)
        private static uint SizeOf(MethodCallInstruction methodCallInstruction) => 5;

        private static uint SizeOf(IndirectMethodCallInstruction indirectMethodCallInstruction) => 5; // calli callsitedescr -> 29 <T> (1 + 4)

        private static uint SizeOf(CreateObjectInstruction createObjectInstruction) => 5; // newobj ctor -> 73 <Token> (1 + 4)

        private static uint SizeOf(InitObjInstruction initObjInstruction) => 6; // initobj typeTok -> FE 15 <Token> (2 + 4) 

        // newobj ctor -> 73 <Token> (1 + 4)
        // newarr etype -> 8D <Token> (1 + 4)
        private static uint SizeOf(CreateArrayInstruction createArrayInstruction) => 5;

        private static uint SizeOf(ConstrainedInstruction constrainedInstruction) => 6; // constrained. thisType -> FE 16 <T> (2 + 4)

        private static uint SizeOf(this BasicInstruction instruction) // fixme rename
        {
            switch (instruction.Operation)
            {
                case BasicOperation.Add:
                case BasicOperation.Sub:
                case BasicOperation.Mul:
                case BasicOperation.Div:
                case BasicOperation.Rem:
                case BasicOperation.And:
                case BasicOperation.Or:
                case BasicOperation.Xor:
                case BasicOperation.Shl:
                case BasicOperation.Shr:
                case BasicOperation.Not:
                case BasicOperation.Neg:
                case BasicOperation.Nop:
                case BasicOperation.Pop:
                case BasicOperation.Dup:
                case BasicOperation.EndFinally:
                case BasicOperation.Throw:
                case BasicOperation.LoadArrayLength:
                case BasicOperation.Breakpoint:
                case BasicOperation.Return:
                    return 1; // 1 Byte OpCode
                case BasicOperation.Eq:
                case BasicOperation.Lt:
                case BasicOperation.Gt:
                case BasicOperation.Rethrow:
                case BasicOperation.EndFilter:
                case BasicOperation.LocalAllocation:
                case BasicOperation.InitBlock:
                case BasicOperation.CopyBlock:
                    return 2; // 2 Byte OpCode
                case BasicOperation.CopyObject:
                    return 5; // cpobj typeTok-> 70 <Token> (1 + 4)
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
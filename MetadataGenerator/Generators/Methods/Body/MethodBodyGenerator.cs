using System;
using System.Collections.Generic;
using MetadataGenerator.Metadata;
using Model;
using Model.Bytecode;
using Model.ThreeAddressCode.Values;
using Model.Types;
using ECMA335 = System.Reflection.Metadata.Ecma335;
using SRM = System.Reflection.Metadata;

namespace MetadataGenerator.Generators.Methods.Body
{
    internal class MethodBodyGenerator
    {
        private readonly MetadataContainer metadataContainer;

        public MethodBodyGenerator(MetadataContainer metadataContainer)
        {
            this.metadataContainer = metadataContainer;
        }

        public ECMA335.InstructionEncoder Generate(MethodBody body)
        {
            var instructionEncoder = new ECMA335.InstructionEncoder(new SRM.BlobBuilder(), new ECMA335.ControlFlowBuilder());
            var controlFlowGenerator = new MethodBodyControlFlowGenerator(instructionEncoder, metadataContainer);
            controlFlowGenerator.ProcessExceptionInformation(body.ExceptionInformation);
            controlFlowGenerator.DefineNeededBranchLabels(body.Instructions);
            var labelToEncoderOffset = new Dictionary<string, int>();
            var switchInstructionsPlaceHolders = new List<SwitchInstructionPlaceholder>();

            foreach (var instruction in body.Instructions)
            {
                labelToEncoderOffset[instruction.Label] = instructionEncoder.Offset;
                controlFlowGenerator.MarkCurrentLabelIfNeeded(instruction.Label);

                switch (instruction)
                {
                    case BasicInstruction basicInstruction:
                        switch (basicInstruction.Operation)
                        {
                            case BasicOperation.Nop:
                                instructionEncoder.OpCode(SRM.ILOpCode.Nop);
                                break;
                            case BasicOperation.Add:
                                if (basicInstruction.OverflowCheck)
                                {
                                    instructionEncoder.OpCode(basicInstruction.UnsignedOperands ? SRM.ILOpCode.Add_ovf_un : SRM.ILOpCode.Add_ovf);
                                }
                                else
                                {
                                    instructionEncoder.OpCode(SRM.ILOpCode.Add);
                                }

                                break;
                            case BasicOperation.Sub:
                                if (basicInstruction.OverflowCheck)
                                {
                                    instructionEncoder.OpCode(basicInstruction.UnsignedOperands ? SRM.ILOpCode.Sub_ovf_un : SRM.ILOpCode.Sub_ovf);
                                }
                                else
                                {
                                    instructionEncoder.OpCode(SRM.ILOpCode.Sub);
                                }

                                break;
                            case BasicOperation.Mul:
                                if (basicInstruction.OverflowCheck)
                                {
                                    instructionEncoder.OpCode(basicInstruction.UnsignedOperands ? SRM.ILOpCode.Mul_ovf_un : SRM.ILOpCode.Mul_ovf);
                                }
                                else
                                {
                                    instructionEncoder.OpCode(SRM.ILOpCode.Mul);
                                }

                                break;
                            case BasicOperation.Div:
                                instructionEncoder.OpCode(basicInstruction.UnsignedOperands ? SRM.ILOpCode.Div_un : SRM.ILOpCode.Div);
                                break;
                            case BasicOperation.Rem:
                                instructionEncoder.OpCode(basicInstruction.UnsignedOperands ? SRM.ILOpCode.Rem_un : SRM.ILOpCode.Rem);
                                break;
                            case BasicOperation.And:
                                instructionEncoder.OpCode(SRM.ILOpCode.And);
                                break;
                            case BasicOperation.Or:
                                instructionEncoder.OpCode(SRM.ILOpCode.Or);
                                break;
                            case BasicOperation.Xor:
                                instructionEncoder.OpCode(SRM.ILOpCode.Xor);
                                break;
                            case BasicOperation.Shl:
                                instructionEncoder.OpCode(SRM.ILOpCode.Shl);
                                break;
                            case BasicOperation.Shr:
                                instructionEncoder.OpCode(basicInstruction.UnsignedOperands ? SRM.ILOpCode.Shr_un : SRM.ILOpCode.Shr);
                                break;
                            case BasicOperation.Eq:
                                instructionEncoder.OpCode(SRM.ILOpCode.Ceq);
                                break;
                            case BasicOperation.Lt:
                                instructionEncoder.OpCode(basicInstruction.UnsignedOperands ? SRM.ILOpCode.Clt_un : SRM.ILOpCode.Clt);
                                break;
                            case BasicOperation.Gt:
                                instructionEncoder.OpCode(basicInstruction.UnsignedOperands ? SRM.ILOpCode.Cgt_un : SRM.ILOpCode.Cgt);
                                break;
                            case BasicOperation.Throw:
                                instructionEncoder.OpCode(SRM.ILOpCode.Throw);
                                break;
                            case BasicOperation.Rethrow:
                                instructionEncoder.OpCode(SRM.ILOpCode.Rethrow);
                                break;
                            case BasicOperation.Not:
                                instructionEncoder.OpCode(SRM.ILOpCode.Not);
                                break;
                            case BasicOperation.Neg:
                                instructionEncoder.OpCode(SRM.ILOpCode.Neg);
                                break;
                            case BasicOperation.Pop:
                                instructionEncoder.OpCode(SRM.ILOpCode.Pop);
                                break;
                            case BasicOperation.Dup:
                                instructionEncoder.OpCode(SRM.ILOpCode.Dup);
                                break;
                            case BasicOperation.EndFinally:
                                instructionEncoder.OpCode(SRM.ILOpCode.Endfinally);
                                break;
                            case BasicOperation.EndFilter:
                                instructionEncoder.OpCode(SRM.ILOpCode.Endfilter);
                                break;
                            case BasicOperation.LocalAllocation:
                                instructionEncoder.OpCode(SRM.ILOpCode.Localloc);
                                break;
                            case BasicOperation.InitBlock:
                                instructionEncoder.OpCode(SRM.ILOpCode.Initblk);
                                break;
                            case BasicOperation.CopyBlock:
                                instructionEncoder.OpCode(SRM.ILOpCode.Cpblk);
                                break;
                            case BasicOperation.LoadArrayLength:
                                instructionEncoder.OpCode(SRM.ILOpCode.Ldlen);
                                break;
                            case BasicOperation.Breakpoint:
                                instructionEncoder.OpCode(SRM.ILOpCode.Break);
                                break;
                            case BasicOperation.Return:
                                instructionEncoder.OpCode(SRM.ILOpCode.Ret);
                                break;
                            default:
                                throw new UnhandledCase();
                        }

                        break;
                    case BranchInstruction branchInstruction:
                    {
                        SRM.ILOpCode opCode;
                        // targets of branch instructions reference other instructions (labels) in the body. But this labels are not going to be
                        // the same as the ones in the final CIL (the ones that instructionEncoder generates) because the instructions in the model
                        // do not know about the size they will occupy in CIL. Due to this, it is not possible to know if the branches could be cil
                        // short forms or not. So regular forms are used in all cases. This does not change functionality, it just means that the 
                        // generated CIL will not be optimal in size.
                        switch (branchInstruction.Operation)
                        {
                            case BranchOperation.False:
                                opCode = SRM.ILOpCode.Brfalse;
                                break;
                            case BranchOperation.True:
                                opCode = SRM.ILOpCode.Brtrue;
                                break;
                            case BranchOperation.Eq:
                                opCode = SRM.ILOpCode.Beq;
                                break;
                            case BranchOperation.Neq:
                                opCode = SRM.ILOpCode.Bne_un;
                                break;
                            case BranchOperation.Lt:
                                opCode = branchInstruction.UnsignedOperands ? SRM.ILOpCode.Blt_un : SRM.ILOpCode.Blt;
                                break;
                            case BranchOperation.Le:
                                opCode = branchInstruction.UnsignedOperands ? SRM.ILOpCode.Ble_un : SRM.ILOpCode.Ble;
                                break;
                            case BranchOperation.Gt:
                                opCode = branchInstruction.UnsignedOperands ? SRM.ILOpCode.Bgt_un : SRM.ILOpCode.Bgt;
                                break;
                            case BranchOperation.Ge:
                                opCode = branchInstruction.UnsignedOperands ? SRM.ILOpCode.Bge_un : SRM.ILOpCode.Bge;
                                break;
                            case BranchOperation.Branch:
                                opCode = SRM.ILOpCode.Br;
                                break;
                            case BranchOperation.Leave:
                                opCode = SRM.ILOpCode.Leave;
                                break;
                            default:
                                throw new UnhandledCase();
                        }

                        instructionEncoder.Branch(opCode, controlFlowGenerator.LabelHandleFor(branchInstruction.Target));

                        break;
                    }

                    case ConvertInstruction convertInstruction:
                        switch (convertInstruction.Operation)
                        {
                            case ConvertOperation.Conv:
                                if (convertInstruction.ConversionType.Equals(PlatformTypes.Int8))
                                {
                                    if (convertInstruction.OverflowCheck)
                                    {
                                        instructionEncoder.OpCode(convertInstruction.UnsignedOperands
                                            ? SRM.ILOpCode.Conv_ovf_i1_un
                                            : SRM.ILOpCode.Conv_ovf_i1);
                                    }
                                    else
                                    {
                                        instructionEncoder.OpCode(SRM.ILOpCode.Conv_i1);
                                    }
                                }
                                else if (convertInstruction.ConversionType.Equals(PlatformTypes.UInt8))
                                {
                                    if (convertInstruction.OverflowCheck)
                                    {
                                        instructionEncoder.OpCode(convertInstruction.UnsignedOperands
                                            ? SRM.ILOpCode.Conv_ovf_u1_un
                                            : SRM.ILOpCode.Conv_ovf_u1);
                                    }
                                    else
                                    {
                                        instructionEncoder.OpCode(SRM.ILOpCode.Conv_u1);
                                    }
                                }
                                else if (convertInstruction.ConversionType.Equals(PlatformTypes.Int16))
                                {
                                    if (convertInstruction.OverflowCheck)
                                    {
                                        instructionEncoder.OpCode(convertInstruction.UnsignedOperands
                                            ? SRM.ILOpCode.Conv_ovf_i2_un
                                            : SRM.ILOpCode.Conv_ovf_i2);
                                    }
                                    else
                                    {
                                        instructionEncoder.OpCode(SRM.ILOpCode.Conv_i2);
                                    }
                                }
                                else if (convertInstruction.ConversionType.Equals(PlatformTypes.UInt16))
                                {
                                    if (convertInstruction.OverflowCheck)
                                    {
                                        instructionEncoder.OpCode(convertInstruction.UnsignedOperands
                                            ? SRM.ILOpCode.Conv_ovf_u2_un
                                            : SRM.ILOpCode.Conv_ovf_u2);
                                    }
                                    else
                                    {
                                        instructionEncoder.OpCode(SRM.ILOpCode.Conv_u2);
                                    }
                                }
                                else if (convertInstruction.ConversionType.Equals(PlatformTypes.Int32))
                                {
                                    if (convertInstruction.OverflowCheck)
                                    {
                                        instructionEncoder.OpCode(convertInstruction.UnsignedOperands
                                            ? SRM.ILOpCode.Conv_ovf_i4_un
                                            : SRM.ILOpCode.Conv_ovf_i4);
                                    }
                                    else
                                    {
                                        instructionEncoder.OpCode(SRM.ILOpCode.Conv_i4);
                                    }
                                }
                                else if (convertInstruction.ConversionType.Equals(PlatformTypes.UInt32))
                                {
                                    if (convertInstruction.OverflowCheck)
                                    {
                                        instructionEncoder.OpCode(convertInstruction.UnsignedOperands
                                            ? SRM.ILOpCode.Conv_ovf_u4_un
                                            : SRM.ILOpCode.Conv_ovf_u4);
                                    }
                                    else
                                    {
                                        instructionEncoder.OpCode(SRM.ILOpCode.Conv_u4);
                                    }
                                }
                                else if (convertInstruction.ConversionType.Equals(PlatformTypes.Int64))
                                {
                                    if (convertInstruction.OverflowCheck)
                                    {
                                        instructionEncoder.OpCode(convertInstruction.UnsignedOperands
                                            ? SRM.ILOpCode.Conv_ovf_i8_un
                                            : SRM.ILOpCode.Conv_ovf_i8);
                                    }
                                    else
                                    {
                                        instructionEncoder.OpCode(SRM.ILOpCode.Conv_i8);
                                    }
                                }
                                else if (convertInstruction.ConversionType.Equals(PlatformTypes.UInt64))
                                {
                                    if (convertInstruction.OverflowCheck)
                                    {
                                        instructionEncoder.OpCode(convertInstruction.UnsignedOperands
                                            ? SRM.ILOpCode.Conv_ovf_u8_un
                                            : SRM.ILOpCode.Conv_ovf_u8);
                                    }
                                    else
                                    {
                                        instructionEncoder.OpCode(SRM.ILOpCode.Conv_u8);
                                    }
                                }
                                else if (convertInstruction.ConversionType.Equals(PlatformTypes.Float32))
                                {
                                    instructionEncoder.OpCode(convertInstruction.UnsignedOperands ? SRM.ILOpCode.Conv_r_un : SRM.ILOpCode.Conv_r4);
                                }
                                else if (convertInstruction.ConversionType.Equals(PlatformTypes.Float64))
                                {
                                    instructionEncoder.OpCode(convertInstruction.UnsignedOperands ? SRM.ILOpCode.Conv_r_un : SRM.ILOpCode.Conv_r8);
                                }
                                else if (convertInstruction.ConversionType.Equals(PlatformTypes.IntPtr))
                                {
                                    if (convertInstruction.OverflowCheck)
                                    {
                                        instructionEncoder.OpCode(convertInstruction.UnsignedOperands
                                            ? SRM.ILOpCode.Conv_ovf_i_un
                                            : SRM.ILOpCode.Conv_ovf_i);
                                    }
                                    else
                                    {
                                        instructionEncoder.OpCode(SRM.ILOpCode.Conv_i);
                                    }
                                }
                                else if (convertInstruction.ConversionType.Equals(PlatformTypes.UIntPtr))
                                {
                                    if (convertInstruction.OverflowCheck)
                                    {
                                        instructionEncoder.OpCode(convertInstruction.UnsignedOperands
                                            ? SRM.ILOpCode.Conv_ovf_u_un
                                            : SRM.ILOpCode.Conv_ovf_u);
                                    }
                                    else
                                    {
                                        instructionEncoder.OpCode(SRM.ILOpCode.Conv_u);
                                    }
                                }
                                else throw new UnhandledCase();

                                break;
                            case ConvertOperation.Cast:
                                instructionEncoder.OpCode(SRM.ILOpCode.Castclass);
                                instructionEncoder.Token(metadataContainer.MetadataResolver.HandleOf(convertInstruction.ConversionType));
                                break;
                            case ConvertOperation.IsInst:
                                instructionEncoder.OpCode(SRM.ILOpCode.Isinst);
                                instructionEncoder.Token(metadataContainer.MetadataResolver.HandleOf(convertInstruction.ConversionType));
                                break;
                            case ConvertOperation.Box:
                                instructionEncoder.OpCode(SRM.ILOpCode.Box);
                                instructionEncoder.Token(metadataContainer.MetadataResolver.HandleOf(convertInstruction.ConversionType));
                                break;
                            case ConvertOperation.Unbox:
                                instructionEncoder.OpCode(SRM.ILOpCode.Unbox_any);
                                instructionEncoder.Token(metadataContainer.MetadataResolver.HandleOf(convertInstruction.ConversionType));
                                break;
                            case ConvertOperation.UnboxPtr:
                                instructionEncoder.OpCode(SRM.ILOpCode.Unbox);
                                instructionEncoder.Token(metadataContainer.MetadataResolver.HandleOf(convertInstruction.ConversionType));
                                break;
                            default:
                                throw new UnhandledCase();
                        }

                        break;
                    case MethodCallInstruction methodCallInstruction:
                        switch (methodCallInstruction.Operation)
                        {
                            case MethodCallOperation.Virtual:
                                instructionEncoder.CallVirtual(metadataContainer.MetadataResolver.HandleOf(methodCallInstruction.Method));
                                break;
                            case MethodCallOperation.Static:
                            case MethodCallOperation.Jump:
                                instructionEncoder.Call(metadataContainer.MetadataResolver.HandleOf(methodCallInstruction.Method));
                                break;
                            default:
                                throw new UnhandledCase();
                        }

                        break;
                    case LoadInstruction loadInstruction:
                    {
                        switch (loadInstruction.Operation)
                        {
                            case LoadOperation.Address:
                            {
                                var variable = (IVariable) loadInstruction.Operand;
                                if (variable.IsParameter)
                                {
                                    var index = body.Parameters.IndexOf(variable);
                                    instructionEncoder.LoadArgumentAddress(index);
                                }
                                else
                                {
                                    var index = body.LocalVariables.IndexOf(variable);
                                    instructionEncoder.LoadLocalAddress(index);
                                }

                                break;
                            }
                            case LoadOperation.Content:
                            {
                                var variable = (IVariable) loadInstruction.Operand;
                                if (variable.IsParameter)
                                {
                                    var index = body.Parameters.IndexOf(variable);
                                    instructionEncoder.LoadArgument(index);
                                }
                                else
                                {
                                    var index = body.LocalVariables.IndexOf(variable);
                                    instructionEncoder.LoadLocal(index);
                                }

                                break;
                            }
                            case LoadOperation.Value:
                            {
                                switch ((loadInstruction.Operand as Constant).Value)
                                {
                                    case null:
                                        instructionEncoder.OpCode(SRM.ILOpCode.Ldnull);
                                        break;
                                    case string value:
                                        instructionEncoder.LoadString(metadataContainer.MetadataBuilder.GetOrAddUserString(value));
                                        break;
                                    case int value:
                                        instructionEncoder.LoadConstantI4(value);
                                        break;
                                    case long value:
                                        instructionEncoder.LoadConstantI8(value);
                                        break;
                                    case float value:
                                        instructionEncoder.LoadConstantR4(value);
                                        break;
                                    case double value:
                                        instructionEncoder.LoadConstantR8(value);
                                        break;
                                    case bool value:
                                        instructionEncoder.LoadConstantI4(value ? 1 : 0);
                                        break;
                                    default:
                                        throw new UnhandledCase();
                                }

                                break;
                            }
                            default:
                                throw new UnhandledCase();
                        }

                        break;
                    }
                    case LoadFieldInstruction loadFieldInstruction:
                        switch (loadFieldInstruction.Operation)
                        {
                            case LoadFieldOperation.Content:
                                instructionEncoder.OpCode(loadFieldInstruction.Field.IsStatic ? SRM.ILOpCode.Ldsfld : SRM.ILOpCode.Ldfld);
                                break;
                            case LoadFieldOperation.Address:
                                instructionEncoder.OpCode(loadFieldInstruction.Field.IsStatic ? SRM.ILOpCode.Ldsflda : SRM.ILOpCode.Ldflda);
                                break;
                            default:
                                throw new UnhandledCase();
                        }

                        instructionEncoder.Token(metadataContainer.MetadataResolver.HandleOf(loadFieldInstruction.Field));
                        break;
                    case LoadArrayElementInstruction loadArrayElementInstruction:
                    {
                        if (loadArrayElementInstruction.Method != null)
                        {
                            instructionEncoder.Call(metadataContainer.MetadataResolver.HandleOf(loadArrayElementInstruction.Method));
                        }
                        else
                        {
                            switch (loadArrayElementInstruction.Operation)
                            {
                                case LoadArrayElementOperation.Content:
                                    if (loadArrayElementInstruction.Array.ElementsType.Equals(PlatformTypes.IntPtr))
                                    {
                                        instructionEncoder.OpCode(SRM.ILOpCode.Ldelem_i);
                                    }
                                    else if (loadArrayElementInstruction.Array.ElementsType.Equals(PlatformTypes.Int8))
                                    {
                                        instructionEncoder.OpCode(SRM.ILOpCode.Ldelem_i1);
                                    }
                                    else if (loadArrayElementInstruction.Array.ElementsType.Equals(PlatformTypes.UInt8))
                                    {
                                        instructionEncoder.OpCode(SRM.ILOpCode.Ldelem_u1);
                                    }
                                    else if (loadArrayElementInstruction.Array.ElementsType.Equals(PlatformTypes.Int16))
                                    {
                                        instructionEncoder.OpCode(SRM.ILOpCode.Ldelem_i2);
                                    }
                                    else if (loadArrayElementInstruction.Array.ElementsType.Equals(PlatformTypes.UInt16))
                                    {
                                        instructionEncoder.OpCode(SRM.ILOpCode.Ldelem_u2);
                                    }
                                    else if (loadArrayElementInstruction.Array.ElementsType.Equals(PlatformTypes.Int32))
                                    {
                                        instructionEncoder.OpCode(SRM.ILOpCode.Ldelem_i4);
                                    }
                                    else if (loadArrayElementInstruction.Array.ElementsType.Equals(PlatformTypes.UInt32))
                                    {
                                        instructionEncoder.OpCode(SRM.ILOpCode.Ldelem_u4);
                                    }
                                    else if (loadArrayElementInstruction.Array.ElementsType.IsOneOf(PlatformTypes.Int64, PlatformTypes.UInt64))
                                    {
                                        instructionEncoder.OpCode(SRM.ILOpCode.Ldelem_i8);
                                    }
                                    else if (loadArrayElementInstruction.Array.ElementsType.Equals(PlatformTypes.Float32))
                                    {
                                        instructionEncoder.OpCode(SRM.ILOpCode.Ldelem_r4);
                                    }
                                    else if (loadArrayElementInstruction.Array.ElementsType.Equals(PlatformTypes.Float64))
                                    {
                                        instructionEncoder.OpCode(SRM.ILOpCode.Ldelem_r8);
                                    }
                                    else if (loadArrayElementInstruction.Array.ElementsType.Equals(PlatformTypes.Object))
                                    {
                                        instructionEncoder.OpCode(SRM.ILOpCode.Ldelem_ref);
                                    }
                                    else
                                    {
                                        instructionEncoder.OpCode(SRM.ILOpCode.Ldelem);
                                        instructionEncoder.Token(
                                            metadataContainer.MetadataResolver.HandleOf(loadArrayElementInstruction.Array.ElementsType));
                                    }

                                    break;
                                case LoadArrayElementOperation.Address:
                                    instructionEncoder.OpCode(SRM.ILOpCode.Ldelema);
                                    instructionEncoder.Token(
                                        metadataContainer.MetadataResolver.HandleOf(loadArrayElementInstruction.Array.ElementsType));
                                    break;

                                default:
                                    throw new UnhandledCase();
                            }
                        }

                        break;
                    }
                    case LoadMethodAddressInstruction loadMethodAddressInstruction:
                        instructionEncoder.OpCode(loadMethodAddressInstruction.Method.IsVirtual ? SRM.ILOpCode.Ldvirtftn : SRM.ILOpCode.Ldftn);
                        instructionEncoder.Token(metadataContainer.MetadataResolver.HandleOf(loadMethodAddressInstruction.Method));
                        break;
                    case CreateArrayInstruction createArrayInstruction:
                        if (createArrayInstruction.Type.IsVector)
                        {
                            instructionEncoder.OpCode(SRM.ILOpCode.Newarr);
                            instructionEncoder.Token(metadataContainer.MetadataResolver.HandleOf(createArrayInstruction.Type.ElementsType));
                        }
                        else
                        {
                            var method = metadataContainer.MetadataResolver.HandleOf(createArrayInstruction.Constructor);
                            instructionEncoder.OpCode(SRM.ILOpCode.Newobj);
                            instructionEncoder.Token(method);
                        }

                        break;
                    case CreateObjectInstruction createObjectInstruction:
                    {
                        var method = metadataContainer.MetadataResolver.HandleOf(createObjectInstruction.Constructor);
                        instructionEncoder.OpCode(SRM.ILOpCode.Newobj);
                        instructionEncoder.Token(method);
                        break;
                    }
                    case StoreInstruction storeInstruction:
                    {
                        var target = storeInstruction.Target;
                        if (target.IsParameter)
                        {
                            var index = body.Parameters.IndexOf(target);
                            instructionEncoder.StoreArgument(index);
                        }
                        else
                        {
                            var index = body.LocalVariables.IndexOf(target);
                            instructionEncoder.StoreLocal(index);
                        }

                        break;
                    }
                    case StoreFieldInstruction storeFieldInstruction:
                        instructionEncoder.OpCode(storeFieldInstruction.Field.IsStatic ? SRM.ILOpCode.Stsfld : SRM.ILOpCode.Stfld);
                        instructionEncoder.Token(metadataContainer.MetadataResolver.HandleOf(storeFieldInstruction.Field));
                        break;
                    case SwitchInstruction switchInstruction:
                    {
                        // switch is encoded as OpCode NumberOfTargets target1, target2, ....
                        // the targets in SwitchInstruction are labels that refer to the Instructions in the method body
                        // but when encoded they must be be offsets relative to the instructionEncoder offsets (real Cil offsets)
                        // this offsets can't be determined until the whole body is generated so a space is reserved for the targets and filled up later
                        var targetsCount = switchInstruction.Targets.Count;
                        instructionEncoder.OpCode(SRM.ILOpCode.Switch);
                        instructionEncoder.Token(targetsCount);
                        var targetsReserveBytes = instructionEncoder.CodeBuilder.ReserveBytes(sizeof(int) * targetsCount);
                        var switchInstructionPlaceholder = new SwitchInstructionPlaceholder(
                            instructionEncoder.Offset,
                            targetsReserveBytes,
                            switchInstruction.Targets);
                        switchInstructionsPlaceHolders.Add(switchInstructionPlaceholder);
                        break;
                    }
                    case SizeofInstruction sizeofInstruction:
                        instructionEncoder.OpCode(SRM.ILOpCode.Sizeof);
                        instructionEncoder.Token(metadataContainer.MetadataResolver.HandleOf(sizeofInstruction.MeasuredType));
                        break;
                    case LoadTokenInstruction loadTokenInstruction:
                        instructionEncoder.OpCode(SRM.ILOpCode.Ldtoken);
                        instructionEncoder.Token(metadataContainer.MetadataResolver.HandleOf(loadTokenInstruction.Token));
                        break;
                    case IndirectMethodCallInstruction indirectMethodCallInstruction:
                        var methodSignature = metadataContainer.MetadataResolver.HandleOf(indirectMethodCallInstruction.Function);
                        instructionEncoder.CallIndirect((SRM.StandaloneSignatureHandle) methodSignature);
                        break;
                    case StoreArrayElementInstruction storeArrayElementInstruction:
                        if (storeArrayElementInstruction.Method != null)
                        {
                            instructionEncoder.Call(metadataContainer.MetadataResolver.HandleOf(storeArrayElementInstruction.Method));
                        }
                        else if (storeArrayElementInstruction.Array.ElementsType.Equals(PlatformTypes.Int8))
                        {
                            instructionEncoder.OpCode(SRM.ILOpCode.Stelem_i1);
                        }
                        else if (storeArrayElementInstruction.Array.ElementsType.Equals(PlatformTypes.Int16))
                        {
                            instructionEncoder.OpCode(SRM.ILOpCode.Stelem_i2);
                        }
                        else if (storeArrayElementInstruction.Array.ElementsType.Equals(PlatformTypes.Int32))
                        {
                            instructionEncoder.OpCode(SRM.ILOpCode.Stelem_i4);
                        }
                        else if (storeArrayElementInstruction.Array.ElementsType.Equals(PlatformTypes.Int64))
                        {
                            instructionEncoder.OpCode(SRM.ILOpCode.Stelem_i8);
                        }
                        else if (storeArrayElementInstruction.Array.ElementsType.Equals(PlatformTypes.Float32))
                        {
                            instructionEncoder.OpCode(SRM.ILOpCode.Stelem_r4);
                        }
                        else if (storeArrayElementInstruction.Array.ElementsType.Equals(PlatformTypes.Float64))
                        {
                            instructionEncoder.OpCode(SRM.ILOpCode.Stelem_r8);
                        }
                        else if (storeArrayElementInstruction.Array.ElementsType.Equals(PlatformTypes.IntPtr))
                        {
                            instructionEncoder.OpCode(SRM.ILOpCode.Stelem_i);
                        }
                        else if (storeArrayElementInstruction.Array.ElementsType.Equals(PlatformTypes.Object))
                        {
                            instructionEncoder.OpCode(SRM.ILOpCode.Stelem_ref);
                        }
                        else
                        {
                            instructionEncoder.OpCode(SRM.ILOpCode.Stelem);
                            instructionEncoder.Token(metadataContainer.MetadataResolver.HandleOf(storeArrayElementInstruction.Array.ElementsType));
                        }

                        break;
                    case InitObjInstruction initObjInstruction:
                        instructionEncoder.OpCode(SRM.ILOpCode.Initobj);
                        instructionEncoder.Token(metadataContainer.MetadataResolver.HandleOf(initObjInstruction.Type));
                        break;
                    case ConstrainedInstruction constrainedInstruction:
                        instructionEncoder.OpCode(SRM.ILOpCode.Constrained);
                        instructionEncoder.Token(metadataContainer.MetadataResolver.HandleOf(constrainedInstruction.ThisType));
                        break;
                    case LoadIndirectInstruction loadIndirectInstruction:
                        if (loadIndirectInstruction.Type.Equals(PlatformTypes.Int8))
                        {
                            instructionEncoder.OpCode(SRM.ILOpCode.Ldind_i1);
                        }
                        else if (loadIndirectInstruction.Type.Equals(PlatformTypes.Int16))
                        {
                            instructionEncoder.OpCode(SRM.ILOpCode.Ldind_i2);
                        }
                        else if (loadIndirectInstruction.Type.Equals(PlatformTypes.Int32))
                        {
                            instructionEncoder.OpCode(SRM.ILOpCode.Ldind_i4);
                        }
                        else if (loadIndirectInstruction.Type.Equals(PlatformTypes.Int64))
                        {
                            instructionEncoder.OpCode(SRM.ILOpCode.Ldind_i8);
                        }
                        else if (loadIndirectInstruction.Type.Equals(PlatformTypes.UInt8))
                        {
                            instructionEncoder.OpCode(SRM.ILOpCode.Ldind_u1);
                        }
                        else if (loadIndirectInstruction.Type.Equals(PlatformTypes.UInt16))
                        {
                            instructionEncoder.OpCode(SRM.ILOpCode.Ldind_u2);
                        }
                        else if (loadIndirectInstruction.Type.Equals(PlatformTypes.UInt32))
                        {
                            instructionEncoder.OpCode(SRM.ILOpCode.Ldind_u4);
                        }
                        else if (loadIndirectInstruction.Type.Equals(PlatformTypes.Float32))
                        {
                            instructionEncoder.OpCode(SRM.ILOpCode.Ldind_r4);
                        }
                        else if (loadIndirectInstruction.Type.Equals(PlatformTypes.Float64))
                        {
                            instructionEncoder.OpCode(SRM.ILOpCode.Ldind_r8);
                        }
                        else if (loadIndirectInstruction.Type.Equals(PlatformTypes.IntPtr))
                        {
                            instructionEncoder.OpCode(SRM.ILOpCode.Ldind_i);
                        }
                        else if (loadIndirectInstruction.Type.Equals(PlatformTypes.Object))
                        {
                            instructionEncoder.OpCode(SRM.ILOpCode.Ldind_ref);
                        }
                        else
                        {
                            instructionEncoder.OpCode(SRM.ILOpCode.Ldobj);
                            instructionEncoder.Token(metadataContainer.MetadataResolver.HandleOf(loadIndirectInstruction.Type));
                        }

                        break;
                    case StoreIndirectInstruction storeIndirectInstruction:
                        if (storeIndirectInstruction.Type.Equals(PlatformTypes.Int8))
                        {
                            instructionEncoder.OpCode(SRM.ILOpCode.Stind_i1);
                        }
                        else if (storeIndirectInstruction.Type.Equals(PlatformTypes.Int16))
                        {
                            instructionEncoder.OpCode(SRM.ILOpCode.Stind_i2);
                        }
                        else if (storeIndirectInstruction.Type.Equals(PlatformTypes.Int32))
                        {
                            instructionEncoder.OpCode(SRM.ILOpCode.Stind_i4);
                        }
                        else if (storeIndirectInstruction.Type.Equals(PlatformTypes.Int64))
                        {
                            instructionEncoder.OpCode(SRM.ILOpCode.Stind_i8);
                        }
                        else if (storeIndirectInstruction.Type.Equals(PlatformTypes.Float32))
                        {
                            instructionEncoder.OpCode(SRM.ILOpCode.Stind_r4);
                        }
                        else if (storeIndirectInstruction.Type.Equals(PlatformTypes.Float64))
                        {
                            instructionEncoder.OpCode(SRM.ILOpCode.Stind_r8);
                        }
                        else if (storeIndirectInstruction.Type.Equals(PlatformTypes.IntPtr))
                        {
                            instructionEncoder.OpCode(SRM.ILOpCode.Stind_i);
                        }
                        else if (storeIndirectInstruction.Type.Equals(PlatformTypes.Object))
                        {
                            instructionEncoder.OpCode(SRM.ILOpCode.Stind_ref);
                        }
                        else
                        {
                            instructionEncoder.OpCode(SRM.ILOpCode.Stobj);
                            instructionEncoder.Token(metadataContainer.MetadataResolver.HandleOf(storeIndirectInstruction.Type));
                        }

                        break;
                    default:
                        throw new UnhandledCase();
                }
            }

            foreach (var switchInstructionPlaceholder in switchInstructionsPlaceHolders)
            {
                switchInstructionPlaceholder.FillWithRealTargets(labelToEncoderOffset);
            }

            return instructionEncoder;
        }

        private class SwitchInstructionPlaceholder
        {
            private readonly int nextInstructionEncoderOffset;
            private readonly SRM.Blob blob;
            private readonly IList<string> targets;

            public SwitchInstructionPlaceholder(int nextInstructionEncoderOffset, SRM.Blob blob, IList<string> targets)
            {
                this.nextInstructionEncoderOffset = nextInstructionEncoderOffset;
                this.blob = blob;
                this.targets = targets;
            }

            // labelToEncoderOffset is the translation of method body labels to the real cil offsets after generation
            public void FillWithRealTargets(IDictionary<string, int> labelToEncoderOffset)
            {
                var writer = new SRM.BlobWriter(blob);
                foreach (var target in targets)
                {
                    // switch targets are offsets relative to the beginning of the next instruction.
                    var offset = labelToEncoderOffset[target] - nextInstructionEncoderOffset;
                    writer.WriteInt32(offset);
                }
            }
        }
    }

    internal class UnhandledCase : Exception
    {
    }
}
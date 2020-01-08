using System;
using System.Globalization;
using System.Linq;
using MetadataGenerator.Metadata;
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

            foreach (var instruction in body.Instructions)
            {
                controlFlowGenerator.MarkCurrentLabel();

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
                            case BasicOperation.InitObject:
                                // FIXME InitObject needs an operand (should not be BasicInstruction). There's already a PR that fixes this
                                break;
                            case BasicOperation.CopyObject:
                                // FIXME CopyObject needs an operand (should not be BasicInstruction)
                                break;
                            case BasicOperation.CopyBlock:
                                instructionEncoder.OpCode(SRM.ILOpCode.Cpblk);
                                break;
                            case BasicOperation.LoadArrayLength:
                                instructionEncoder.OpCode(SRM.ILOpCode.Ldlen);
                                break;
                            case BasicOperation.IndirectLoad:
                                // FIXME IndirectLoad needs an operand (should not be BasicInstruction). There's already a PR that fixes this
                                // example is already generated for all variants
                                break;
                            case BasicOperation.IndirectStore:
                                // FIXME IndirectStore needs an operand (should not be BasicInstruction). There's already a PR that fixes this
                                break;
                            case BasicOperation.Breakpoint:
                                instructionEncoder.OpCode(SRM.ILOpCode.Break);
                                break;
                            case BasicOperation.Return:
                                instructionEncoder.OpCode(SRM.ILOpCode.Ret);
                                break;
                        }

                        break;
                    case BranchInstruction branchInstruction:
                    {
                        var opCode = SRM.ILOpCode.Br_s;
                        switch (branchInstruction.Operation)
                        {
                            // TODO all short forms can also be not short form (ex: br and br.s)
                            case BranchOperation.False:
                                opCode = SRM.ILOpCode.Brfalse_s;
                                break;
                            case BranchOperation.True:
                                opCode = SRM.ILOpCode.Brtrue_s;
                                break;
                            case BranchOperation.Eq:
                                opCode = SRM.ILOpCode.Beq_s;
                                break;
                            case BranchOperation.Neq:
                                opCode = SRM.ILOpCode.Bne_un_s;
                                break;
                            case BranchOperation.Lt:
                                opCode = branchInstruction.UnsignedOperands ? SRM.ILOpCode.Blt_un_s : SRM.ILOpCode.Blt_s;
                                break;
                            case BranchOperation.Le:
                                opCode = branchInstruction.UnsignedOperands ? SRM.ILOpCode.Ble_un_s : SRM.ILOpCode.Ble_s;
                                break;
                            case BranchOperation.Gt:
                                opCode = branchInstruction.UnsignedOperands ? SRM.ILOpCode.Bgt_un_s : SRM.ILOpCode.Bgt_s;
                                break;
                            case BranchOperation.Ge:
                                opCode = branchInstruction.UnsignedOperands ? SRM.ILOpCode.Bge_un_s : SRM.ILOpCode.Bge_s;
                                break;
                            case BranchOperation.Branch:
                                opCode = SRM.ILOpCode.Br_s;
                                break;
                            case BranchOperation.Leave:
                                opCode = SRM.ILOpCode.Leave_s;
                                break;
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

                                break;
                            case ConvertOperation.Cast:
                                instructionEncoder.OpCode(SRM.ILOpCode.Castclass);
                                instructionEncoder.Token(metadataContainer.metadataResolver.HandleOf(convertInstruction.ConversionType));
                                break;
                            case ConvertOperation.IsInst:
                                instructionEncoder.OpCode(SRM.ILOpCode.Isinst);
                                instructionEncoder.Token(metadataContainer.metadataResolver.HandleOf(convertInstruction.ConversionType));
                                break;
                            case ConvertOperation.Box:
                                instructionEncoder.OpCode(SRM.ILOpCode.Box);
                                instructionEncoder.Token(metadataContainer.metadataResolver.HandleOf(convertInstruction.ConversionType));
                                break;
                            case ConvertOperation.Unbox:
                                instructionEncoder.OpCode(SRM.ILOpCode.Unbox_any);
                                instructionEncoder.Token(metadataContainer.metadataResolver.HandleOf(convertInstruction.ConversionType));
                                break;
                            case ConvertOperation.UnboxPtr:
                                instructionEncoder.OpCode(SRM.ILOpCode.Unbox);
                                instructionEncoder.Token(metadataContainer.metadataResolver.HandleOf(convertInstruction.ConversionType));
                                break;
                        }

                        break;
                    case MethodCallInstruction methodCallInstruction:
                        switch (methodCallInstruction.Operation)
                        {
                            case MethodCallOperation.Virtual:
                                instructionEncoder.CallVirtual(metadataContainer.metadataResolver.HandleOf(methodCallInstruction.Method));
                                break;
                            case MethodCallOperation.Static:
                            case MethodCallOperation.Jump:
                                instructionEncoder.Call(metadataContainer.metadataResolver.HandleOf(methodCallInstruction.Method));
                                break;
                        }

                        break;
                    case LoadInstruction loadInstruction:
                        switch (loadInstruction.Operation)
                        {
                            case LoadOperation.Address:
                                var operandVariable = (IVariable) loadInstruction.Operand;
                                if (operandVariable.IsParameter)
                                {
                                    instructionEncoder.LoadArgumentAddress(body.Parameters.IndexOf(operandVariable));
                                }
                                else
                                {
                                    instructionEncoder.LoadLocalAddress(body.LocalVariables.IndexOf(operandVariable));
                                }

                                break;
                            case LoadOperation.Content:
                                operandVariable = (IVariable) loadInstruction.Operand;
                                if (operandVariable.IsParameter)
                                {
                                    instructionEncoder.LoadArgument(body.Parameters.IndexOf(operandVariable));
                                }
                                else
                                {
                                    instructionEncoder.LoadLocal(body.LocalVariables.IndexOf(operandVariable));
                                }

                                break;
                            case LoadOperation.Value:
                                if (((Constant) loadInstruction.Operand).Value == null)
                                {
                                    instructionEncoder.OpCode(SRM.ILOpCode.Ldnull);
                                }

                                if (loadInstruction.Operand.Type.Equals(PlatformTypes.String))
                                {
                                    var value = (string) (loadInstruction.Operand as Constant).Value;
                                    instructionEncoder.LoadString(metadataContainer.metadataBuilder.GetOrAddUserString(value));
                                }

                                else if (loadInstruction.Operand.Type.IsOneOf(PlatformTypes.Int8, PlatformTypes.UInt8))
                                {
                                    var value = (int) (loadInstruction.Operand as Constant).Value;
                                    instructionEncoder.OpCode(SRM.ILOpCode.Ldc_i4_s);
                                    instructionEncoder.Token(value);
                                }
                                else if (loadInstruction.Operand.Type.IsOneOf(PlatformTypes.Int16, PlatformTypes.Int32, PlatformTypes.UInt16,
                                    PlatformTypes.UInt32))
                                {
                                    var value = (int) (loadInstruction.Operand as Constant).Value;
                                    instructionEncoder.LoadConstantI4(value);
                                }
                                else if (loadInstruction.Operand.Type.IsOneOf(PlatformTypes.Int64, PlatformTypes.UInt64))
                                {
                                    var value = (long) (loadInstruction.Operand as Constant).Value;
                                    instructionEncoder.LoadConstantI8(value);
                                }
                                else if (loadInstruction.Operand.Type.Equals(PlatformTypes.Float32))
                                {
                                    var value = (float) (loadInstruction.Operand as Constant).Value;
                                    instructionEncoder.LoadConstantR4(value);
                                }
                                else if (loadInstruction.Operand.Type.Equals(PlatformTypes.Float64))
                                {
                                    var value = (double) (loadInstruction.Operand as Constant).Value;
                                    instructionEncoder.LoadConstantR8(value);
                                }

                                break;
                        }

                        break;
                    case LoadFieldInstruction loadFieldInstruction:
                        switch (loadFieldInstruction.Operation)
                        {
                            case LoadFieldOperation.Content:
                                instructionEncoder.OpCode(SRM.ILOpCode.Ldfld);
                                break;
                            case LoadFieldOperation.Address:
                                // TODO test. Example present but not supported in model
                                instructionEncoder.OpCode(SRM.ILOpCode.Ldflda);
                                break;
                        }

                        instructionEncoder.Token(metadataContainer.metadataResolver.HandleOf(loadFieldInstruction.Field));
                        break;
                    case LoadArrayElementInstruction loadArrayElementInstruction:
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
                                        metadataContainer.metadataResolver.HandleOf(loadArrayElementInstruction.Array.ElementsType));
                                }

                                break;
                            case LoadArrayElementOperation.Address:
                                instructionEncoder.OpCode(SRM.ILOpCode.Ldelema);
                                instructionEncoder.Token(metadataContainer.metadataResolver.HandleOf(loadArrayElementInstruction.Array.ElementsType));
                                break;
                        }

                        break;
                    case LoadMethodAddressInstruction loadMethodAddressInstruction:
                        instructionEncoder.OpCode(SRM.ILOpCode.Ldftn);
                        instructionEncoder.Token(metadataContainer.metadataResolver.HandleOf(loadMethodAddressInstruction.Method));
                        break;
                    case CreateArrayInstruction createArrayInstruction:
                        if (createArrayInstruction.Type.IsVector)
                        {
                            instructionEncoder.OpCode(SRM.ILOpCode.Newarr);
                            instructionEncoder.Token(metadataContainer.metadataResolver.HandleOf(createArrayInstruction.Type.ElementsType));
                        }
                        else
                        {
                            throw new Exception("newarr only handles one dimension and zero based arrays");
                        }

                        break;
                    case CreateObjectInstruction createObjectInstruction:
                        var method = metadataContainer.metadataResolver.HandleOf(createObjectInstruction.Constructor);
                        instructionEncoder.OpCode(SRM.ILOpCode.Newobj);
                        instructionEncoder.Token(method);
                        break;
                    case StoreInstruction storeInstruction:
                        if (storeInstruction.Target.IsParameter)
                        {
                            instructionEncoder.StoreArgument(body.Parameters.IndexOf(storeInstruction.Target));
                        }
                        else
                        {
                            instructionEncoder.StoreLocal(body.LocalVariables.IndexOf(storeInstruction.Target));
                        }

                        break;
                    case StoreFieldInstruction storeFieldInstruction:
                        instructionEncoder.OpCode(storeFieldInstruction.Field.IsStatic ? SRM.ILOpCode.Stsfld : SRM.ILOpCode.Stfld);
                        instructionEncoder.Token(metadataContainer.metadataResolver.HandleOf(storeFieldInstruction.Field));
                        break;
                    case SwitchInstruction switchInstruction:
                        instructionEncoder.OpCode(SRM.ILOpCode.Switch);
                        instructionEncoder.Token(switchInstruction.Targets.Count);
                        switchInstruction.Targets
                            .Select(label => int.Parse(label.Substring(2), NumberStyles.HexNumber))
                            .ToList()
                            .ForEach(instructionEncoder.Token);
                        break;
                    case SizeofInstruction sizeofInstruction:
                        instructionEncoder.OpCode(SRM.ILOpCode.Sizeof);
                        instructionEncoder.Token(metadataContainer.metadataResolver.HandleOf(sizeofInstruction.MeasuredType));
                        break;
                    case LoadTokenInstruction loadTokenInstruction:
                        instructionEncoder.OpCode(SRM.ILOpCode.Ldtoken);
                        instructionEncoder.Token(metadataContainer.metadataResolver.HandleOf(loadTokenInstruction.Token));
                        break;
                    case IndirectMethodCallInstruction indirectMethodCallInstruction:
                        var methodSignature = metadataContainer.metadataResolver.HandleOf(indirectMethodCallInstruction.Function);
                        instructionEncoder.CallIndirect((SRM.StandaloneSignatureHandle) methodSignature);
                        break;
                    case StoreArrayElementInstruction storeArrayElementInstruction:
                        if (storeArrayElementInstruction.Array.ElementsType.Equals(PlatformTypes.Int8))
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
                            instructionEncoder.Token(metadataContainer.metadataResolver.HandleOf(storeArrayElementInstruction.Array.ElementsType));
                        }

                        break;
                    default:
                        throw new Exception("instruction type not handled");
                }
            }

            controlFlowGenerator.MarkAllUnmarkedLabels(); // FIXME  remove

            return instructionEncoder;
        }
    }
}
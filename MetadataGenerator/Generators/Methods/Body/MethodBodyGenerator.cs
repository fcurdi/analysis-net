using System;
using MetadataGenerator.Generators.Methods.Body;
using Model.ThreeAddressCode.Values;
using Model.Types;
using ECMA335 = System.Reflection.Metadata.Ecma335;
using SRM = System.Reflection.Metadata;

namespace MetadataGenerator.Generators.Methods
{
    // review all instructions of the ecma pdf
    // review overflow and other checks that instructions have
    class MethodBodyGenerator
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

                if (instruction is Model.Bytecode.BasicInstruction basicInstruction)
                {
                    switch (basicInstruction.Operation)
                    {
                        // TODO 
                        // check overflow for variants (ej: add, add_ovf, add_ovf_un)
                        // see all IlOpCode constants and ECMA

                        case Model.Bytecode.BasicOperation.Nop:
                            instructionEncoder.OpCode(SRM.ILOpCode.Nop);
                            break;
                        case Model.Bytecode.BasicOperation.Add:
                            instructionEncoder.OpCode(SRM.ILOpCode.Add);
                            break;
                        case Model.Bytecode.BasicOperation.Sub:
                            instructionEncoder.OpCode(SRM.ILOpCode.Sub);
                            break;
                        case Model.Bytecode.BasicOperation.Mul:
                            instructionEncoder.OpCode(SRM.ILOpCode.Mul);
                            break;
                        case Model.Bytecode.BasicOperation.Div:
                            instructionEncoder.OpCode(SRM.ILOpCode.Div);
                            break;
                        case Model.Bytecode.BasicOperation.Rem:
                            instructionEncoder.OpCode(SRM.ILOpCode.Rem);
                            break;
                        case Model.Bytecode.BasicOperation.And:
                            instructionEncoder.OpCode(SRM.ILOpCode.And);
                            break;
                        case Model.Bytecode.BasicOperation.Or:
                            instructionEncoder.OpCode(SRM.ILOpCode.Or);
                            break;
                        case Model.Bytecode.BasicOperation.Xor:
                            instructionEncoder.OpCode(SRM.ILOpCode.Xor);
                            break;
                        case Model.Bytecode.BasicOperation.Shl:
                            instructionEncoder.OpCode(SRM.ILOpCode.Shl);
                            break;
                        case Model.Bytecode.BasicOperation.Shr:
                            instructionEncoder.OpCode(SRM.ILOpCode.Shr);
                            break;
                        case Model.Bytecode.BasicOperation.Eq:
                            instructionEncoder.OpCode(SRM.ILOpCode.Ceq);
                            break;
                        case Model.Bytecode.BasicOperation.Lt:
                            instructionEncoder.OpCode(SRM.ILOpCode.Clt);
                            break;
                        case Model.Bytecode.BasicOperation.Gt:
                            instructionEncoder.OpCode(SRM.ILOpCode.Cgt);
                            break;
                        case Model.Bytecode.BasicOperation.Throw:
                            instructionEncoder.OpCode(SRM.ILOpCode.Throw);
                            break;
                        case Model.Bytecode.BasicOperation.Rethrow:
                            instructionEncoder.OpCode(SRM.ILOpCode.Rethrow);
                            break;
                        case Model.Bytecode.BasicOperation.Not:
                            instructionEncoder.OpCode(SRM.ILOpCode.Not);
                            break;
                        case Model.Bytecode.BasicOperation.Neg:
                            instructionEncoder.OpCode(SRM.ILOpCode.Neg);
                            break;
                        case Model.Bytecode.BasicOperation.Pop:
                            instructionEncoder.OpCode(SRM.ILOpCode.Pop);
                            break;
                        case Model.Bytecode.BasicOperation.Dup:
                            instructionEncoder.OpCode(SRM.ILOpCode.Dup);
                            break;
                        case Model.Bytecode.BasicOperation.EndFinally:
                            instructionEncoder.OpCode(SRM.ILOpCode.Endfinally);
                            break;
                        case Model.Bytecode.BasicOperation.EndFilter:
                            instructionEncoder.OpCode(SRM.ILOpCode.Endfilter);
                            break;
                        case Model.Bytecode.BasicOperation.LocalAllocation:
                            instructionEncoder.OpCode(SRM.ILOpCode.Localloc);
                            break;
                        case Model.Bytecode.BasicOperation.InitBlock:
                            instructionEncoder.OpCode(SRM.ILOpCode.Initblk);
                            break;
                        case Model.Bytecode.BasicOperation.InitObject:
                            // FIXME InitObject needs an operand (should not be BasicInstruction)
                            // instructionEncoder.OpCode(ILOpCode.Initobj);
                            // instructionEncoder.Token(type)
                            break;
                        case Model.Bytecode.BasicOperation.CopyObject:
                            // FIXME CopyObject needs an operand (should not be BasicInstruction)
                            break;
                        case Model.Bytecode.BasicOperation.CopyBlock:
                            instructionEncoder.OpCode(SRM.ILOpCode.Cpblk);
                            break;
                        case Model.Bytecode.BasicOperation.LoadArrayLength:
                            instructionEncoder.OpCode(SRM.ILOpCode.Ldlen);
                            break;
                        case Model.Bytecode.BasicOperation.IndirectLoad:
                            // FIXME IndirectLoad needs an operand (should not be BasicInstruction)
                            // TODO depending on type of operand instructionEncoder.OpCode(SRM.ILOpCode.Ldind_X);
                            // example is already generated for all variants
                            break;
                        case Model.Bytecode.BasicOperation.LoadArrayElement:
                            // FIXME LoadArrayElement needs an operand (should not be BasicInstruction)
                            // TODO depending on type of operand instructionEncoder.OpCode(SRM.ILOpCode.Ldelem_X);
                            // example is already generated
                            break;
                        case Model.Bytecode.BasicOperation.LoadArrayElementAddress:
                            // FIXME LoadArrayElementAddress needs an operand (should not be BasicInstruction)
                            // instructionEncoder.OpCode(SRM.ILOpCode.Ldelema);
                            // instructionEncoder.token(type);
                            // example is already generated
                            break;
                        case Model.Bytecode.BasicOperation.IndirectStore:
                            // FIXME IndirectStore needs an operand (should not be BasicInstruction)
                            // instructionEncoder.OpCode(SRM.ILOpCode.Stobj);
                            // instructionEncoder.token();
                            break;
                        case Model.Bytecode.BasicOperation.StoreArrayElement:
                            // FIXME StoreArrayElement needs an operand (should not be BasicInstruction)
                            // instructionEncoder.OpCode(SRM.ILOpCode.Stelem_X);

                            // instructionEncoder.OpCode(SRM.ILOpCode.Stelem);
                            // instructionEncoder.token();
                            break;
                        case Model.Bytecode.BasicOperation.Breakpoint:
                            instructionEncoder.OpCode(SRM.ILOpCode.Break);
                            break;
                        case Model.Bytecode.BasicOperation.Return:
                            instructionEncoder.OpCode(SRM.ILOpCode.Ret);
                            break;
                    }
                }
                else if (instruction is Model.Bytecode.BranchInstruction branchInstruction)
                {
                    var opCode = SRM.ILOpCode.Br_s;
                    switch (branchInstruction.Operation)
                    {
                        // This relies on marking labels and that depends on generating all instructions correctly (if not labels don't match)
                        // TODO all short forms can also be not short form (ex: br and br.s) and there's "un" variants
                        case Model.Bytecode.BranchOperation.False:
                            opCode = SRM.ILOpCode.Brfalse_s;
                            break;
                        case Model.Bytecode.BranchOperation.True:
                            opCode = SRM.ILOpCode.Brtrue_s;
                            break;
                        case Model.Bytecode.BranchOperation.Eq:
                            opCode = SRM.ILOpCode.Beq_s;
                            break;
                        case Model.Bytecode.BranchOperation.Neq:
                            opCode = SRM.ILOpCode.Bne_un_s;
                            break;
                        case Model.Bytecode.BranchOperation.Lt:
                            opCode = SRM.ILOpCode.Blt_s;
                            break;
                        case Model.Bytecode.BranchOperation.Le:
                            opCode = SRM.ILOpCode.Ble_s;
                            break;
                        case Model.Bytecode.BranchOperation.Gt:
                            opCode = SRM.ILOpCode.Bgt_s;
                            break;
                        case Model.Bytecode.BranchOperation.Ge:
                            opCode = SRM.ILOpCode.Bge_s;
                            break;
                        case Model.Bytecode.BranchOperation.Branch:
                            opCode = SRM.ILOpCode.Br_s;
                            break;
                        case Model.Bytecode.BranchOperation.Leave:
                            opCode = SRM.ILOpCode.Leave_s;
                            break;
                    }
                    instructionEncoder.Branch(opCode, controlFlowGenerator.LabelHandleFor(branchInstruction.Target));
                }
                else if (instruction is Model.Bytecode.ConvertInstruction convertInstruction)
                {
                    switch (convertInstruction.Operation)
                    {
                        case Model.Bytecode.ConvertOperation.Conv:
                            // TODO
                            break;
                        case Model.Bytecode.ConvertOperation.Cast:
                            instructionEncoder.OpCode(SRM.ILOpCode.Castclass);
                            // FIXME could also be instructionEncoder.OpCode(SRM.ILOpCode.Isinst);
                            break;
                        case Model.Bytecode.ConvertOperation.Box:
                            instructionEncoder.OpCode(SRM.ILOpCode.Box);
                            break;
                        case Model.Bytecode.ConvertOperation.Unbox:
                            instructionEncoder.OpCode(SRM.ILOpCode.Unbox_any);
                            break;
                        case Model.Bytecode.ConvertOperation.UnboxPtr:
                            instructionEncoder.OpCode(SRM.ILOpCode.Unbox);
                            break;
                    }
                    instructionEncoder.Token(metadataContainer.ResolveReferenceHandleFor(convertInstruction.ConversionType));
                }
                else if (instruction is Model.Bytecode.MethodCallInstruction methodCallInstruction)
                {
                    switch (methodCallInstruction.Operation)
                    {
                        case Model.Bytecode.MethodCallOperation.Virtual:
                            instructionEncoder.CallVirtual(metadataContainer.ResolveReferenceHandleFor(methodCallInstruction.Method));
                            break;
                        case Model.Bytecode.MethodCallOperation.Static:
                        case Model.Bytecode.MethodCallOperation.Jump:
                            instructionEncoder.Call(metadataContainer.ResolveReferenceHandleFor(methodCallInstruction.Method));
                            break;
                    }
                }
                else if (instruction is Model.Bytecode.LoadInstruction loadlInstruction)
                {
                    switch (loadlInstruction.Operation)
                    {
                        case Model.Bytecode.LoadOperation.Address:
                            var operandVariable = (IVariable)loadlInstruction.Operand;
                            if (operandVariable.IsParameter)
                            {
                                instructionEncoder.LoadArgumentAddress(body.Parameters.IndexOf(operandVariable));
                            }
                            else
                            {
                                instructionEncoder.LoadLocalAddress(body.LocalVariables.IndexOf(operandVariable));
                            }
                            break;
                        case Model.Bytecode.LoadOperation.Content:
                            operandVariable = (IVariable)loadlInstruction.Operand;
                            if (operandVariable.IsParameter)
                            {
                                instructionEncoder.LoadArgument(body.Parameters.IndexOf(operandVariable));
                            }
                            else
                            {
                                instructionEncoder.LoadLocal(body.LocalVariables.IndexOf(operandVariable));
                            }
                            break;
                        case Model.Bytecode.LoadOperation.Value:
                            if (((Constant)loadlInstruction.Operand).Value == null)
                            {
                                instructionEncoder.OpCode(SRM.ILOpCode.Ldnull);
                            }
                            if (loadlInstruction.Operand.Type.Equals(PlatformTypes.String))
                            {
                                var value = (string)(loadlInstruction.Operand as Constant).Value;
                                instructionEncoder.LoadString(metadataContainer.metadataBuilder.GetOrAddUserString(value));
                            }

                            // TODO see ECMA ldc instruction. It says some cases should be follow by conv.i8 operations but the bytecode (original) does not have that
                            else if (loadlInstruction.Operand.Type.Equals(PlatformTypes.Int8) || loadlInstruction.Operand.Type.Equals(PlatformTypes.UInt8))
                            {
                                var value = (int)(loadlInstruction.Operand as Constant).Value;
                                instructionEncoder.OpCode(SRM.ILOpCode.Ldc_i4_s);
                                instructionEncoder.Token(value);
                                // FIXME: do only if value variable storing the 8 bit number is 8 byte integer.
                                // instructionEncoder.OpCode(SRM.ILOpCode.Conv_i8);
                            }
                            else if (
                                loadlInstruction.Operand.Type.Equals(PlatformTypes.Int16) ||
                                loadlInstruction.Operand.Type.Equals(PlatformTypes.Int32) ||
                                loadlInstruction.Operand.Type.Equals(PlatformTypes.UInt16) ||
                                loadlInstruction.Operand.Type.Equals(PlatformTypes.UInt32))
                            {
                                var value = (int)(loadlInstruction.Operand as Constant).Value;
                                instructionEncoder.LoadConstantI4(value);
                                // FIXME: do only if value variable storing the 16/32 bit number is 8 byte integer.
                                //    instructionEncoder.OpCode(SRM.ILOpCode.Conv_i8);
                            }
                            else if (loadlInstruction.Operand.Type.Equals(PlatformTypes.Int64) || loadlInstruction.Operand.Type.Equals(PlatformTypes.UInt64))
                            {
                                var value = (long)(loadlInstruction.Operand as Constant).Value;
                                instructionEncoder.LoadConstantI8(value);
                            }
                            else if (loadlInstruction.Operand.Type.Equals(PlatformTypes.Float32))
                            {
                                var value = (float)(loadlInstruction.Operand as Constant).Value;
                                instructionEncoder.LoadConstantR4(value);
                            }
                            else if (loadlInstruction.Operand.Type.Equals(PlatformTypes.Float64))
                            {
                                var value = (double)(loadlInstruction.Operand as Constant).Value;
                                instructionEncoder.LoadConstantR8(value);
                            }
                            break;
                    }
                }
                else if (instruction is Model.Bytecode.LoadFieldInstruction loadFieldInstruction)
                {
                    // TODO handle ldflda. Example present but not supported in model

                    instructionEncoder.OpCode(SRM.ILOpCode.Ldfld);
                    instructionEncoder.Token(metadataContainer.ResolveReferenceHandleFor(loadFieldInstruction.Field));
                }
                else if (instruction is Model.Bytecode.LoadArrayElementInstruction loadArrayElementInstruction) { }
                else if (instruction is Model.Bytecode.LoadMethodAddressInstruction loadMethodAdressInstruction)
                {
                    instructionEncoder.OpCode(SRM.ILOpCode.Ldftn);
                    instructionEncoder.Token(metadataContainer.ResolveReferenceHandleFor(loadMethodAdressInstruction.Method));
                }
                else if (instruction is Model.Bytecode.CreateArrayInstruction createArrayInstruction)
                {
                    if (createArrayInstruction.Type.IsVector)
                    {
                        instructionEncoder.OpCode(SRM.ILOpCode.Newarr);
                        instructionEncoder.Token(metadataContainer.ResolveReferenceHandleFor(createArrayInstruction.Type.ElementsType));
                    }
                    else
                    {
                        throw new Exception("newarr only handles one dimension and zero based arrays");
                    }
                }
                else if (instruction is Model.Bytecode.CreateObjectInstruction createObjectInstruction)
                {
                    var method = metadataContainer.ResolveReferenceHandleFor(createObjectInstruction.Constructor);
                    instructionEncoder.OpCode(SRM.ILOpCode.Newobj);
                    instructionEncoder.Token(method);
                }
                else if (instruction is Model.Bytecode.StoreInstruction storeInstruction)
                {
                    if (storeInstruction.Target.IsParameter)
                    {
                        instructionEncoder.StoreArgument(body.Parameters.IndexOf(storeInstruction.Target));
                    }
                    else
                    {
                        instructionEncoder.StoreLocal(body.LocalVariables.IndexOf(storeInstruction.Target));
                    }
                }
                else if (instruction is Model.Bytecode.StoreFieldInstruction storeFieldInstruction)
                {
                    instructionEncoder.OpCode(storeFieldInstruction.Field.IsStatic ? SRM.ILOpCode.Stsfld : SRM.ILOpCode.Stfld);
                    instructionEncoder.Token(metadataContainer.ResolveReferenceHandleFor(storeFieldInstruction.Field));
                }
                else if (instruction is Model.Bytecode.SwitchInstruction switchInstruction) { }
                else if (instruction is Model.Bytecode.SizeofInstruction sizeofInstruction)
                {
                    instructionEncoder.OpCode(SRM.ILOpCode.Sizeof);
                    instructionEncoder.Token(metadataContainer.ResolveReferenceHandleFor(sizeofInstruction.MeasuredType));
                }
                else if (instruction is Model.Bytecode.LoadTokenInstruction loadTokenInstruction)
                {
                    instructionEncoder.OpCode(SRM.ILOpCode.Ldtoken);
                    instructionEncoder.Token(metadataContainer.ResolveReferenceHandleFor(loadTokenInstruction.Token));
                }
                else if (instruction is Model.Bytecode.IndirectMethodCallInstruction indirectMethodCallInstruction) { }
                else if (instruction is Model.Bytecode.StoreArrayElementInstruction storeArrayElementInstruction) { }
                else throw new Exception("instruction type not handled");
            }

            controlFlowGenerator.MarkAllUnmarkedLabels();

            return instructionEncoder;
        }
    }
}

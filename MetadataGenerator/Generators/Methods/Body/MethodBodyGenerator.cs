﻿using System;
using System.Collections.Generic;
using MetadataGenerator.Metadata;
using Model;
using Model.Bytecode;
using Model.Bytecode.Visitor;
using Model.ThreeAddressCode.Values;
using Model.Types;
using ECMA335 = System.Reflection.Metadata.Ecma335;
using SRM = System.Reflection.Metadata;

namespace MetadataGenerator.Generators.Methods.Body
{
    internal class MethodBodyGenerator : IInstructionVisitor
    {
        private readonly MetadataContainer metadataContainer;
        private readonly ECMA335.InstructionEncoder instructionEncoder;
        private readonly MethodBodyControlFlowGenerator controlFlowGenerator;
        private readonly List<SwitchInstructionPlaceholder> switchInstructionsPlaceHolders;
        private readonly ISet<int> ignoredInstructions;
        private readonly MethodBody body;
        private int Index { get; set; }

        public MethodBodyGenerator(MetadataContainer metadataContainer, MethodBody body)
        {
            this.metadataContainer = metadataContainer;
            this.body = body;
            instructionEncoder = new ECMA335.InstructionEncoder(new SRM.BlobBuilder(), new ECMA335.ControlFlowBuilder());
            controlFlowGenerator = new MethodBodyControlFlowGenerator(instructionEncoder, metadataContainer);
            switchInstructionsPlaceHolders = new List<SwitchInstructionPlaceholder>();
            ignoredInstructions = new HashSet<int>();
        }

        public ECMA335.InstructionEncoder Generate()
        {
            controlFlowGenerator.ProcessExceptionInformation(body.ExceptionInformation);
            controlFlowGenerator.DefineNeededBranchLabels(body.Instructions);
            var labelToEncoderOffset = new Dictionary<string, int>();

            for (Index = 0; Index < body.Instructions.Count; Index++)
            {
                var instruction = (Instruction) body.Instructions[Index];
                labelToEncoderOffset[instruction.Label] = instructionEncoder.Offset;
                controlFlowGenerator.MarkCurrentLabelIfNeeded(instruction.Label);

                if (!ignoredInstructions.Contains(Index))
                {
                    instruction.Accept(this);
                }
            }

            foreach (var switchInstructionPlaceholder in switchInstructionsPlaceHolders)
            {
                switchInstructionPlaceholder.FillWithRealTargets(labelToEncoderOffset);
            }

            return instructionEncoder;
        }

        public void Visit(IInstructionContainer container)
        {
        }

        public void Visit(Instruction instruction)
        {
        }

        public void Visit(InitObjInstruction instruction)
        {
            instructionEncoder.OpCode(SRM.ILOpCode.Initobj);
            instructionEncoder.Token(metadataContainer.MetadataResolver.HandleOf(instruction.Type));
        }

        public void Visit(BasicInstruction instruction)
        {
            switch (instruction.Operation)
            {
                case BasicOperation.Nop:
                    instructionEncoder.OpCode(SRM.ILOpCode.Nop);
                    break;
                case BasicOperation.Add:
                    if (instruction.OverflowCheck)
                    {
                        instructionEncoder.OpCode(instruction.UnsignedOperands ? SRM.ILOpCode.Add_ovf_un : SRM.ILOpCode.Add_ovf);
                    }
                    else
                    {
                        instructionEncoder.OpCode(SRM.ILOpCode.Add);
                    }

                    break;
                case BasicOperation.Sub:
                    if (instruction.OverflowCheck)
                    {
                        instructionEncoder.OpCode(instruction.UnsignedOperands ? SRM.ILOpCode.Sub_ovf_un : SRM.ILOpCode.Sub_ovf);
                    }
                    else
                    {
                        instructionEncoder.OpCode(SRM.ILOpCode.Sub);
                    }

                    break;
                case BasicOperation.Mul:
                    if (instruction.OverflowCheck)
                    {
                        instructionEncoder.OpCode(instruction.UnsignedOperands ? SRM.ILOpCode.Mul_ovf_un : SRM.ILOpCode.Mul_ovf);
                    }
                    else
                    {
                        instructionEncoder.OpCode(SRM.ILOpCode.Mul);
                    }

                    break;
                case BasicOperation.Div:
                    instructionEncoder.OpCode(instruction.UnsignedOperands ? SRM.ILOpCode.Div_un : SRM.ILOpCode.Div);
                    break;
                case BasicOperation.Rem:
                    instructionEncoder.OpCode(instruction.UnsignedOperands ? SRM.ILOpCode.Rem_un : SRM.ILOpCode.Rem);
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
                    instructionEncoder.OpCode(instruction.UnsignedOperands ? SRM.ILOpCode.Shr_un : SRM.ILOpCode.Shr);
                    break;
                case BasicOperation.Eq:
                    instructionEncoder.OpCode(SRM.ILOpCode.Ceq);
                    break;
                case BasicOperation.Lt:
                    instructionEncoder.OpCode(instruction.UnsignedOperands ? SRM.ILOpCode.Clt_un : SRM.ILOpCode.Clt);
                    break;
                case BasicOperation.Gt:
                    instructionEncoder.OpCode(instruction.UnsignedOperands ? SRM.ILOpCode.Cgt_un : SRM.ILOpCode.Cgt);
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
        }

        public void Visit(ConstrainedInstruction instruction)
        {
            instructionEncoder.OpCode(SRM.ILOpCode.Constrained);
            instructionEncoder.Token(metadataContainer.MetadataResolver.HandleOf(instruction.ThisType));
        }

        public void Visit(LoadInstruction instruction)
        {
            switch (instruction.Operation)
            {
                case LoadOperation.Address:
                {
                    var variable = (IVariable) instruction.Operand;
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
                    var variable = (IVariable) instruction.Operand;
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
                    switch (((Constant) instruction.Operand).Value)
                    {
                        case null:
                            instructionEncoder.OpCode(SRM.ILOpCode.Ldnull);

                            if (
                                body.Instructions.Count > Index + 2 &&
                                body.Instructions[Index + 1] is BasicInstruction i1 && i1.Operation == BasicOperation.Eq &&
                                body.Instructions[Index + 2] is BasicInstruction i2 && i2.Operation == BasicOperation.Neg
                            )
                            {
                                // cgt_un is used as a compare-not-equal with null.
                                // load null - compare eq - negate => ldnull - cgt_un
                                instructionEncoder.OpCode(SRM.ILOpCode.Cgt_un);

                                // skip processing next 2 instructions
                                ignoredInstructions.Add(Index + 1);
                                ignoredInstructions.Add(Index + 2);
                            }

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
        }

        public void Visit(LoadIndirectInstruction instruction)
        {
            if (instruction.Type.Equals(PlatformTypes.Int8))
            {
                instructionEncoder.OpCode(SRM.ILOpCode.Ldind_i1);
            }
            else if (instruction.Type.Equals(PlatformTypes.Int16))
            {
                instructionEncoder.OpCode(SRM.ILOpCode.Ldind_i2);
            }
            else if (instruction.Type.Equals(PlatformTypes.Int32))
            {
                instructionEncoder.OpCode(SRM.ILOpCode.Ldind_i4);
            }
            else if (instruction.Type.Equals(PlatformTypes.Int64))
            {
                instructionEncoder.OpCode(SRM.ILOpCode.Ldind_i8);
            }
            else if (instruction.Type.Equals(PlatformTypes.UInt8))
            {
                instructionEncoder.OpCode(SRM.ILOpCode.Ldind_u1);
            }
            else if (instruction.Type.Equals(PlatformTypes.UInt16))
            {
                instructionEncoder.OpCode(SRM.ILOpCode.Ldind_u2);
            }
            else if (instruction.Type.Equals(PlatformTypes.UInt32))
            {
                instructionEncoder.OpCode(SRM.ILOpCode.Ldind_u4);
            }
            else if (instruction.Type.Equals(PlatformTypes.Float32))
            {
                instructionEncoder.OpCode(SRM.ILOpCode.Ldind_r4);
            }
            else if (instruction.Type.Equals(PlatformTypes.Float64))
            {
                instructionEncoder.OpCode(SRM.ILOpCode.Ldind_r8);
            }
            else if (instruction.Type.Equals(PlatformTypes.IntPtr))
            {
                instructionEncoder.OpCode(SRM.ILOpCode.Ldind_i);
            }
            else if (instruction.Type.Equals(PlatformTypes.Object))
            {
                instructionEncoder.OpCode(SRM.ILOpCode.Ldind_ref);
            }
            else
            {
                instructionEncoder.OpCode(SRM.ILOpCode.Ldobj);
                instructionEncoder.Token(metadataContainer.MetadataResolver.HandleOf(instruction.Type));
            }
        }

        public void Visit(LoadFieldInstruction instruction)
        {
            switch (instruction.Operation)
            {
                case LoadFieldOperation.Content:
                    instructionEncoder.OpCode(instruction.Field.IsStatic ? SRM.ILOpCode.Ldsfld : SRM.ILOpCode.Ldfld);
                    break;
                case LoadFieldOperation.Address:
                    instructionEncoder.OpCode(instruction.Field.IsStatic ? SRM.ILOpCode.Ldsflda : SRM.ILOpCode.Ldflda);
                    break;
                default:
                    throw new UnhandledCase();
            }

            instructionEncoder.Token(metadataContainer.MetadataResolver.HandleOf(instruction.Field));
        }

        public void Visit(LoadMethodAddressInstruction instruction)
        {
            instructionEncoder.OpCode(instruction.Method.IsVirtual ? SRM.ILOpCode.Ldvirtftn : SRM.ILOpCode.Ldftn);
            instructionEncoder.Token(metadataContainer.MetadataResolver.HandleOf(instruction.Method));
        }

        public void Visit(StoreIndirectInstruction instruction)
        {
            if (instruction.Type.Equals(PlatformTypes.Int8))
            {
                instructionEncoder.OpCode(SRM.ILOpCode.Stind_i1);
            }
            else if (instruction.Type.Equals(PlatformTypes.Int16))
            {
                instructionEncoder.OpCode(SRM.ILOpCode.Stind_i2);
            }
            else if (instruction.Type.Equals(PlatformTypes.Int32))
            {
                instructionEncoder.OpCode(SRM.ILOpCode.Stind_i4);
            }
            else if (instruction.Type.Equals(PlatformTypes.Int64))
            {
                instructionEncoder.OpCode(SRM.ILOpCode.Stind_i8);
            }
            else if (instruction.Type.Equals(PlatformTypes.Float32))
            {
                instructionEncoder.OpCode(SRM.ILOpCode.Stind_r4);
            }
            else if (instruction.Type.Equals(PlatformTypes.Float64))
            {
                instructionEncoder.OpCode(SRM.ILOpCode.Stind_r8);
            }
            else if (instruction.Type.Equals(PlatformTypes.IntPtr))
            {
                instructionEncoder.OpCode(SRM.ILOpCode.Stind_i);
            }
            else if (instruction.Type.Equals(PlatformTypes.Object))
            {
                instructionEncoder.OpCode(SRM.ILOpCode.Stind_ref);
            }
            else
            {
                instructionEncoder.OpCode(SRM.ILOpCode.Stobj);
                instructionEncoder.Token(metadataContainer.MetadataResolver.HandleOf(instruction.Type));
            }
        }

        public void Visit(StoreInstruction instruction)
        {
            var target = instruction.Target;
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
        }

        public void Visit(StoreFieldInstruction instruction)
        {
            instructionEncoder.OpCode(instruction.Field.IsStatic ? SRM.ILOpCode.Stsfld : SRM.ILOpCode.Stfld);
            instructionEncoder.Token(metadataContainer.MetadataResolver.HandleOf(instruction.Field));
        }

        public void Visit(ConvertInstruction instruction)
        {
            switch (instruction.Operation)
            {
                case ConvertOperation.Conv:
                    if (instruction.ConversionType.Equals(PlatformTypes.Int8))
                    {
                        if (instruction.OverflowCheck)
                        {
                            instructionEncoder.OpCode(instruction.UnsignedOperands
                                ? SRM.ILOpCode.Conv_ovf_i1_un
                                : SRM.ILOpCode.Conv_ovf_i1);
                        }
                        else
                        {
                            instructionEncoder.OpCode(SRM.ILOpCode.Conv_i1);
                        }
                    }
                    else if (instruction.ConversionType.Equals(PlatformTypes.UInt8))
                    {
                        if (instruction.OverflowCheck)
                        {
                            instructionEncoder.OpCode(instruction.UnsignedOperands
                                ? SRM.ILOpCode.Conv_ovf_u1_un
                                : SRM.ILOpCode.Conv_ovf_u1);
                        }
                        else
                        {
                            instructionEncoder.OpCode(SRM.ILOpCode.Conv_u1);
                        }
                    }
                    else if (instruction.ConversionType.Equals(PlatformTypes.Int16))
                    {
                        if (instruction.OverflowCheck)
                        {
                            instructionEncoder.OpCode(instruction.UnsignedOperands
                                ? SRM.ILOpCode.Conv_ovf_i2_un
                                : SRM.ILOpCode.Conv_ovf_i2);
                        }
                        else
                        {
                            instructionEncoder.OpCode(SRM.ILOpCode.Conv_i2);
                        }
                    }
                    else if (instruction.ConversionType.Equals(PlatformTypes.UInt16))
                    {
                        if (instruction.OverflowCheck)
                        {
                            instructionEncoder.OpCode(instruction.UnsignedOperands
                                ? SRM.ILOpCode.Conv_ovf_u2_un
                                : SRM.ILOpCode.Conv_ovf_u2);
                        }
                        else
                        {
                            instructionEncoder.OpCode(SRM.ILOpCode.Conv_u2);
                        }
                    }
                    else if (instruction.ConversionType.Equals(PlatformTypes.Int32))
                    {
                        if (instruction.OverflowCheck)
                        {
                            instructionEncoder.OpCode(instruction.UnsignedOperands
                                ? SRM.ILOpCode.Conv_ovf_i4_un
                                : SRM.ILOpCode.Conv_ovf_i4);
                        }
                        else
                        {
                            instructionEncoder.OpCode(SRM.ILOpCode.Conv_i4);
                        }
                    }
                    else if (instruction.ConversionType.Equals(PlatformTypes.UInt32))
                    {
                        if (instruction.OverflowCheck)
                        {
                            instructionEncoder.OpCode(instruction.UnsignedOperands
                                ? SRM.ILOpCode.Conv_ovf_u4_un
                                : SRM.ILOpCode.Conv_ovf_u4);
                        }
                        else
                        {
                            instructionEncoder.OpCode(SRM.ILOpCode.Conv_u4);
                        }
                    }
                    else if (instruction.ConversionType.Equals(PlatformTypes.Int64))
                    {
                        if (instruction.OverflowCheck)
                        {
                            instructionEncoder.OpCode(instruction.UnsignedOperands
                                ? SRM.ILOpCode.Conv_ovf_i8_un
                                : SRM.ILOpCode.Conv_ovf_i8);
                        }
                        else
                        {
                            instructionEncoder.OpCode(SRM.ILOpCode.Conv_i8);
                        }
                    }
                    else if (instruction.ConversionType.Equals(PlatformTypes.UInt64))
                    {
                        if (instruction.OverflowCheck)
                        {
                            instructionEncoder.OpCode(instruction.UnsignedOperands
                                ? SRM.ILOpCode.Conv_ovf_u8_un
                                : SRM.ILOpCode.Conv_ovf_u8);
                        }
                        else
                        {
                            instructionEncoder.OpCode(SRM.ILOpCode.Conv_u8);
                        }
                    }
                    else if (instruction.ConversionType.Equals(PlatformTypes.Float32))
                    {
                        instructionEncoder.OpCode(instruction.UnsignedOperands ? SRM.ILOpCode.Conv_r_un : SRM.ILOpCode.Conv_r4);
                    }
                    else if (instruction.ConversionType.Equals(PlatformTypes.Float64))
                    {
                        instructionEncoder.OpCode(instruction.UnsignedOperands ? SRM.ILOpCode.Conv_r_un : SRM.ILOpCode.Conv_r8);
                    }
                    else if (instruction.ConversionType.Equals(PlatformTypes.IntPtr))
                    {
                        if (instruction.OverflowCheck)
                        {
                            instructionEncoder.OpCode(instruction.UnsignedOperands
                                ? SRM.ILOpCode.Conv_ovf_i_un
                                : SRM.ILOpCode.Conv_ovf_i);
                        }
                        else
                        {
                            instructionEncoder.OpCode(SRM.ILOpCode.Conv_i);
                        }
                    }
                    else if (instruction.ConversionType.Equals(PlatformTypes.UIntPtr))
                    {
                        if (instruction.OverflowCheck)
                        {
                            instructionEncoder.OpCode(instruction.UnsignedOperands
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
                    instructionEncoder.Token(metadataContainer.MetadataResolver.HandleOf(instruction.ConversionType));
                    break;
                case ConvertOperation.IsInst:
                    instructionEncoder.OpCode(SRM.ILOpCode.Isinst);
                    instructionEncoder.Token(metadataContainer.MetadataResolver.HandleOf(instruction.ConversionType));
                    break;
                case ConvertOperation.Box:
                    instructionEncoder.OpCode(SRM.ILOpCode.Box);
                    instructionEncoder.Token(metadataContainer.MetadataResolver.HandleOf(instruction.ConversionType));
                    break;
                case ConvertOperation.Unbox:
                    instructionEncoder.OpCode(SRM.ILOpCode.Unbox_any);
                    instructionEncoder.Token(metadataContainer.MetadataResolver.HandleOf(instruction.ConversionType));
                    break;
                case ConvertOperation.UnboxPtr:
                    instructionEncoder.OpCode(SRM.ILOpCode.Unbox);
                    instructionEncoder.Token(metadataContainer.MetadataResolver.HandleOf(instruction.ConversionType));
                    break;
                default:
                    throw new UnhandledCase();
            }
        }

        public void Visit(BranchInstruction instruction)
        {
            SRM.ILOpCode opCode;
            // targets of branch instructions reference other instructions (labels) in the body. But this labels are not going to be
            // the same as the ones in the final CIL (the ones that instructionEncoder generates) because the instructions in the model
            // do not know about the size they will occupy in CIL. Due to this, it is not possible to know if the branches could be cil
            // short forms or not. So regular forms are used in all cases. This does not change functionality, it just means that the 
            // generated CIL will not be optimal in size.
            switch (instruction.Operation)
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
                    opCode = instruction.UnsignedOperands ? SRM.ILOpCode.Blt_un : SRM.ILOpCode.Blt;
                    break;
                case BranchOperation.Le:
                    opCode = instruction.UnsignedOperands ? SRM.ILOpCode.Ble_un : SRM.ILOpCode.Ble;
                    break;
                case BranchOperation.Gt:
                    opCode = instruction.UnsignedOperands ? SRM.ILOpCode.Bgt_un : SRM.ILOpCode.Bgt;
                    break;
                case BranchOperation.Ge:
                    opCode = instruction.UnsignedOperands ? SRM.ILOpCode.Bge_un : SRM.ILOpCode.Bge;
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

            instructionEncoder.Branch(opCode, controlFlowGenerator.LabelHandleFor(instruction.Target));
        }

        public void Visit(SwitchInstruction instruction)
        {
            // switch is encoded as OpCode NumberOfTargets target1, target2, ....
            // the targets in SwitchInstruction are labels that refer to the Instructions in the method body
            // but when encoded they must be be offsets relative to the instructionEncoder offsets (real Cil offsets)
            // this offsets can't be determined until the whole body is generated so a space is reserved for the targets and filled up later
            var targetsCount = instruction.Targets.Count;
            instructionEncoder.OpCode(SRM.ILOpCode.Switch);
            instructionEncoder.Token(targetsCount);
            var targetsReserveBytes = instructionEncoder.CodeBuilder.ReserveBytes(sizeof(int) * targetsCount);
            var switchInstructionPlaceholder = new SwitchInstructionPlaceholder(
                instructionEncoder.Offset,
                targetsReserveBytes,
                instruction.Targets);
            switchInstructionsPlaceHolders.Add(switchInstructionPlaceholder);
        }

        public void Visit(SizeofInstruction instruction)
        {
            instructionEncoder.OpCode(SRM.ILOpCode.Sizeof);
            instructionEncoder.Token(metadataContainer.MetadataResolver.HandleOf(instruction.MeasuredType));
        }

        public void Visit(LoadTokenInstruction instruction)
        {
            instructionEncoder.OpCode(SRM.ILOpCode.Ldtoken);
            instructionEncoder.Token(metadataContainer.MetadataResolver.HandleOf(instruction.Token));
        }

        public void Visit(MethodCallInstruction instruction)
        {
            switch (instruction.Operation)
            {
                case MethodCallOperation.Virtual:
                    instructionEncoder.CallVirtual(metadataContainer.MetadataResolver.HandleOf(instruction.Method));
                    break;
                case MethodCallOperation.Static:
                case MethodCallOperation.Jump:
                    instructionEncoder.Call(metadataContainer.MetadataResolver.HandleOf(instruction.Method));
                    break;
                default:
                    throw new UnhandledCase();
            }
        }

        public void Visit(IndirectMethodCallInstruction instruction)
        {
            var methodSignature = metadataContainer.MetadataResolver.HandleOf(instruction.Function);
            instructionEncoder.CallIndirect((SRM.StandaloneSignatureHandle) methodSignature);
        }

        public void Visit(CreateObjectInstruction instruction)
        {
            var method = metadataContainer.MetadataResolver.HandleOf(instruction.Constructor);
            instructionEncoder.OpCode(SRM.ILOpCode.Newobj);
            instructionEncoder.Token(method);
        }

        public void Visit(CreateArrayInstruction instruction)
        {
            if (instruction.Type.IsVector)
            {
                instructionEncoder.OpCode(SRM.ILOpCode.Newarr);
                instructionEncoder.Token(metadataContainer.MetadataResolver.HandleOf(instruction.Type.ElementsType));
            }
            else
            {
                var method = metadataContainer.MetadataResolver.HandleOf(instruction.Constructor);
                instructionEncoder.OpCode(SRM.ILOpCode.Newobj);
                instructionEncoder.Token(method);
            }
        }

        public void Visit(LoadArrayElementInstruction instruction)
        {
            if (instruction.Method != null)
            {
                instructionEncoder.Call(metadataContainer.MetadataResolver.HandleOf(instruction.Method));
            }
            else
            {
                switch (instruction.Operation)
                {
                    case LoadArrayElementOperation.Content:
                        if (instruction.Array.ElementsType.Equals(PlatformTypes.IntPtr))
                        {
                            instructionEncoder.OpCode(SRM.ILOpCode.Ldelem_i);
                        }
                        else if (instruction.Array.ElementsType.Equals(PlatformTypes.Int8))
                        {
                            instructionEncoder.OpCode(SRM.ILOpCode.Ldelem_i1);
                        }
                        else if (instruction.Array.ElementsType.Equals(PlatformTypes.UInt8))
                        {
                            instructionEncoder.OpCode(SRM.ILOpCode.Ldelem_u1);
                        }
                        else if (instruction.Array.ElementsType.Equals(PlatformTypes.Int16))
                        {
                            instructionEncoder.OpCode(SRM.ILOpCode.Ldelem_i2);
                        }
                        else if (instruction.Array.ElementsType.Equals(PlatformTypes.UInt16))
                        {
                            instructionEncoder.OpCode(SRM.ILOpCode.Ldelem_u2);
                        }
                        else if (instruction.Array.ElementsType.Equals(PlatformTypes.Int32))
                        {
                            instructionEncoder.OpCode(SRM.ILOpCode.Ldelem_i4);
                        }
                        else if (instruction.Array.ElementsType.Equals(PlatformTypes.UInt32))
                        {
                            instructionEncoder.OpCode(SRM.ILOpCode.Ldelem_u4);
                        }
                        else if (instruction.Array.ElementsType.Equals(PlatformTypes.Int64) ||
                                 instruction.Array.ElementsType.Equals(PlatformTypes.UInt64))
                        {
                            instructionEncoder.OpCode(SRM.ILOpCode.Ldelem_i8);
                        }
                        else if (instruction.Array.ElementsType.Equals(PlatformTypes.Float32))
                        {
                            instructionEncoder.OpCode(SRM.ILOpCode.Ldelem_r4);
                        }
                        else if (instruction.Array.ElementsType.Equals(PlatformTypes.Float64))
                        {
                            instructionEncoder.OpCode(SRM.ILOpCode.Ldelem_r8);
                        }
                        else if (instruction.Array.ElementsType.Equals(PlatformTypes.Object))
                        {
                            instructionEncoder.OpCode(SRM.ILOpCode.Ldelem_ref);
                        }
                        else
                        {
                            instructionEncoder.OpCode(SRM.ILOpCode.Ldelem);
                            instructionEncoder.Token(
                                metadataContainer.MetadataResolver.HandleOf(instruction.Array.ElementsType));
                        }

                        break;
                    case LoadArrayElementOperation.Address:
                        instructionEncoder.OpCode(SRM.ILOpCode.Ldelema);
                        instructionEncoder.Token(
                            metadataContainer.MetadataResolver.HandleOf(instruction.Array.ElementsType));
                        break;

                    default:
                        throw new UnhandledCase();
                }
            }
        }

        public void Visit(StoreArrayElementInstruction instruction)
        {
            if (instruction.Method != null)
            {
                instructionEncoder.Call(metadataContainer.MetadataResolver.HandleOf(instruction.Method));
            }
            else if (instruction.Array.ElementsType.Equals(PlatformTypes.Int8))
            {
                instructionEncoder.OpCode(SRM.ILOpCode.Stelem_i1);
            }
            else if (instruction.Array.ElementsType.Equals(PlatformTypes.Int16))
            {
                instructionEncoder.OpCode(SRM.ILOpCode.Stelem_i2);
            }
            else if (instruction.Array.ElementsType.Equals(PlatformTypes.Int32))
            {
                instructionEncoder.OpCode(SRM.ILOpCode.Stelem_i4);
            }
            else if (instruction.Array.ElementsType.Equals(PlatformTypes.Int64))
            {
                instructionEncoder.OpCode(SRM.ILOpCode.Stelem_i8);
            }
            else if (instruction.Array.ElementsType.Equals(PlatformTypes.Float32))
            {
                instructionEncoder.OpCode(SRM.ILOpCode.Stelem_r4);
            }
            else if (instruction.Array.ElementsType.Equals(PlatformTypes.Float64))
            {
                instructionEncoder.OpCode(SRM.ILOpCode.Stelem_r8);
            }
            else if (instruction.Array.ElementsType.Equals(PlatformTypes.IntPtr))
            {
                instructionEncoder.OpCode(SRM.ILOpCode.Stelem_i);
            }
            else if (instruction.Array.ElementsType.Equals(PlatformTypes.Object))
            {
                instructionEncoder.OpCode(SRM.ILOpCode.Stelem_ref);
            }
            else
            {
                instructionEncoder.OpCode(SRM.ILOpCode.Stelem);
                instructionEncoder.Token(metadataContainer.MetadataResolver.HandleOf(instruction.Array.ElementsType));
            }
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
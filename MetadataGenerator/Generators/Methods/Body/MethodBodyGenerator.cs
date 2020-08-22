using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly StackSize stackSize;
        private int Index { get; set; }

        public MethodBodyGenerator(MetadataContainer metadataContainer, MethodBody body)
        {
            this.metadataContainer = metadataContainer;
            this.body = body;
            instructionEncoder = new ECMA335.InstructionEncoder(new SRM.BlobBuilder(), new ECMA335.ControlFlowBuilder());
            controlFlowGenerator = new MethodBodyControlFlowGenerator(instructionEncoder, metadataContainer);
            switchInstructionsPlaceHolders = new List<SwitchInstructionPlaceholder>();
            ignoredInstructions = new HashSet<int>();
            stackSize = new StackSize();
        }

        public ECMA335.InstructionEncoder Generate(out int maxStack)
        {
            controlFlowGenerator.ProcessExceptionInformation(body.ExceptionInformation);
            controlFlowGenerator.DefineNeededBranchLabels(body.Instructions);
            var labelToEncoderOffset = new Dictionary<string, int>();

            for (Index = 0; Index < body.Instructions.Count; Index++)
            {
                var instruction = (Instruction) body.Instructions[Index];
                labelToEncoderOffset[instruction.Label] = instructionEncoder.Offset;
                controlFlowGenerator.MarkCurrentLabelIfNeeded(instruction.Label);

                if (body.ExceptionInformation.Any(block => // FIXME metodo aparte
                {
                    switch (block.Handler)
                    {
                        case FilterExceptionHandler filterExceptionHandler:
                            return filterExceptionHandler.FilterStart.Equals(instruction.Label) ||
                                   filterExceptionHandler.Start.Equals(instruction.Label);
                        case CatchExceptionHandler catchExceptionHandler:
                            return catchExceptionHandler.Start.Equals(instruction.Label);
                        default:
                            return false;
                    }
                }))
                {
                    stackSize.Increment();
                }

                if (!ignoredInstructions.Contains(Index))
                {
                    instruction.Accept(this);
                }
            }

            foreach (var switchInstructionPlaceholder in switchInstructionsPlaceHolders)
            {
                switchInstructionPlaceholder.FillWithRealTargets(labelToEncoderOffset);
            }

            // FIXME revienta
            maxStack = stackSize.MaxStackSize;
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
            stackSize.Decrement();
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

                    stackSize.Decrement();

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

                    stackSize.Decrement();
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

                    stackSize.Decrement();
                    break;
                case BasicOperation.Div:
                    instructionEncoder.OpCode(instruction.UnsignedOperands ? SRM.ILOpCode.Div_un : SRM.ILOpCode.Div);
                    stackSize.Decrement();
                    break;
                case BasicOperation.Rem:
                    instructionEncoder.OpCode(instruction.UnsignedOperands ? SRM.ILOpCode.Rem_un : SRM.ILOpCode.Rem);
                    stackSize.Decrement();
                    break;
                case BasicOperation.And:
                    instructionEncoder.OpCode(SRM.ILOpCode.And);
                    stackSize.Decrement();
                    break;
                case BasicOperation.Or:
                    instructionEncoder.OpCode(SRM.ILOpCode.Or);
                    stackSize.Decrement();
                    break;
                case BasicOperation.Xor:
                    instructionEncoder.OpCode(SRM.ILOpCode.Xor);
                    stackSize.Decrement();
                    break;
                case BasicOperation.Shl:
                    instructionEncoder.OpCode(SRM.ILOpCode.Shl);
                    stackSize.Decrement();
                    break;
                case BasicOperation.Shr:
                    instructionEncoder.OpCode(instruction.UnsignedOperands ? SRM.ILOpCode.Shr_un : SRM.ILOpCode.Shr);
                    stackSize.Decrement();
                    break;
                case BasicOperation.Eq:
                    instructionEncoder.OpCode(SRM.ILOpCode.Ceq);
                    stackSize.Decrement();
                    break;
                case BasicOperation.Lt:
                    instructionEncoder.OpCode(instruction.UnsignedOperands ? SRM.ILOpCode.Clt_un : SRM.ILOpCode.Clt);
                    stackSize.Decrement();
                    break;
                case BasicOperation.Gt:
                    instructionEncoder.OpCode(instruction.UnsignedOperands ? SRM.ILOpCode.Cgt_un : SRM.ILOpCode.Cgt);
                    stackSize.Decrement();
                    break;
                case BasicOperation.Throw:
                    instructionEncoder.OpCode(SRM.ILOpCode.Throw);
                    stackSize.Clear();
                    break;
                case BasicOperation.Rethrow:
                    instructionEncoder.OpCode(SRM.ILOpCode.Rethrow);
                    stackSize.Clear();
                    break;
                case BasicOperation.Not:
                    instructionEncoder.OpCode(SRM.ILOpCode.Not);
                    break;
                case BasicOperation.Neg:
                    instructionEncoder.OpCode(SRM.ILOpCode.Neg);
                    break;
                case BasicOperation.Pop:
                    instructionEncoder.OpCode(SRM.ILOpCode.Pop);
                    stackSize.Decrement();
                    break;
                case BasicOperation.Dup:
                    instructionEncoder.OpCode(SRM.ILOpCode.Dup);
                    stackSize.Increment();
                    break;
                case BasicOperation.EndFinally:
                    instructionEncoder.OpCode(SRM.ILOpCode.Endfinally);
                    stackSize.Clear();
                    break;
                case BasicOperation.EndFilter:
                    instructionEncoder.OpCode(SRM.ILOpCode.Endfilter);
                    stackSize.Clear();
                    break;
                case BasicOperation.LocalAllocation:
                    instructionEncoder.OpCode(SRM.ILOpCode.Localloc);
                    break;
                case BasicOperation.InitBlock:
                    instructionEncoder.OpCode(SRM.ILOpCode.Initblk);
                    stackSize.Decrement(3);
                    break;
                case BasicOperation.CopyBlock:
                    instructionEncoder.OpCode(SRM.ILOpCode.Cpblk);
                    stackSize.Decrement(3);
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

                    stackSize.Increment();

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

                    stackSize.Increment();

                    break;
                }
                case LoadOperation.Value:
                {
                    switch (((Constant) instruction.Operand).Value)
                    {
                        case null:
                            instructionEncoder.OpCode(SRM.ILOpCode.Ldnull);
                            stackSize.Increment();

                            if (
                                body.Instructions.Count > Index + 2 &&
                                body.Instructions[Index + 1] is BasicInstruction i1 && i1.Operation == BasicOperation.Eq &&
                                body.Instructions[Index + 2] is BasicInstruction i2 && i2.Operation == BasicOperation.Neg
                            )
                            {
                                // cgt_un is used as a compare-not-equal with null.
                                // load null - compare eq - negate => ldnull - cgt_un
                                instructionEncoder.OpCode(SRM.ILOpCode.Cgt_un);
                                stackSize.Decrement();

                                // skip processing next 2 instructions
                                ignoredInstructions.Add(Index + 1);
                                ignoredInstructions.Add(Index + 2);
                            }

                            break;
                        case string value:
                            instructionEncoder.LoadString(metadataContainer.MetadataBuilder.GetOrAddUserString(value));
                            stackSize.Increment();
                            break;
                        case int value:
                            instructionEncoder.LoadConstantI4(value);
                            stackSize.Increment();
                            break;
                        case long value:
                            instructionEncoder.LoadConstantI8(value);
                            stackSize.Increment();
                            break;
                        case float value:
                            instructionEncoder.LoadConstantR4(value);
                            stackSize.Increment();
                            break;
                        case double value:
                            instructionEncoder.LoadConstantR8(value);
                            stackSize.Increment();
                            break;
                        case bool value:
                            instructionEncoder.LoadConstantI4(value ? 1 : 0);
                            stackSize.Increment();
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
            var isStatic = instruction.Field.IsStatic;
            switch (instruction.Operation)
            {
                case LoadFieldOperation.Content:
                    instructionEncoder.OpCode(isStatic ? SRM.ILOpCode.Ldsfld : SRM.ILOpCode.Ldfld);
                    break;
                case LoadFieldOperation.Address:
                    instructionEncoder.OpCode(isStatic ? SRM.ILOpCode.Ldsflda : SRM.ILOpCode.Ldflda);
                    break;
                default:
                    throw new UnhandledCase();
            }

            instructionEncoder.Token(metadataContainer.MetadataResolver.HandleOf(instruction.Field));
            if (isStatic)
            {
                stackSize.Increment();
            }
        }

        public void Visit(LoadMethodAddressInstruction instruction)
        {
            var isVirtual = instruction.Method.IsVirtual;
            instructionEncoder.OpCode(isVirtual ? SRM.ILOpCode.Ldvirtftn : SRM.ILOpCode.Ldftn);
            instructionEncoder.Token(metadataContainer.MetadataResolver.HandleOf(instruction.Method));
            if (!isVirtual)
            {
                stackSize.Increment();
            }
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

            stackSize.Decrement(2);
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

            stackSize.Decrement();
        }

        public void Visit(StoreFieldInstruction instruction)
        {
            var isStatic = instruction.Field.IsStatic;
            instructionEncoder.OpCode(isStatic ? SRM.ILOpCode.Stsfld : SRM.ILOpCode.Stfld);
            instructionEncoder.Token(metadataContainer.MetadataResolver.HandleOf(instruction.Field));
            stackSize.Decrement(isStatic ? 1 : 2);
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
                    stackSize.Decrement(2);
                    break;
                case BranchOperation.Neq:
                    opCode = SRM.ILOpCode.Bne_un;
                    stackSize.Decrement(2);
                    break;
                case BranchOperation.Lt:
                    opCode = instruction.UnsignedOperands ? SRM.ILOpCode.Blt_un : SRM.ILOpCode.Blt;
                    stackSize.Decrement(2);
                    break;
                case BranchOperation.Le:
                    opCode = instruction.UnsignedOperands ? SRM.ILOpCode.Ble_un : SRM.ILOpCode.Ble;
                    stackSize.Decrement(2);
                    break;
                case BranchOperation.Gt:
                    opCode = instruction.UnsignedOperands ? SRM.ILOpCode.Bgt_un : SRM.ILOpCode.Bgt;
                    stackSize.Decrement(2);
                    break;
                case BranchOperation.Ge:
                    opCode = instruction.UnsignedOperands ? SRM.ILOpCode.Bge_un : SRM.ILOpCode.Bge;
                    stackSize.Decrement(2);
                    break;
                case BranchOperation.Branch:
                    opCode = SRM.ILOpCode.Br;
                    break;
                case BranchOperation.Leave:
                    opCode = SRM.ILOpCode.Leave;
                    stackSize.Clear();
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
            stackSize.Decrement();
        }

        public void Visit(SizeofInstruction instruction)
        {
            instructionEncoder.OpCode(SRM.ILOpCode.Sizeof);
            instructionEncoder.Token(metadataContainer.MetadataResolver.HandleOf(instruction.MeasuredType));
            stackSize.Increment();
        }

        public void Visit(LoadTokenInstruction instruction)
        {
            instructionEncoder.OpCode(SRM.ILOpCode.Ldtoken);
            instructionEncoder.Token(metadataContainer.MetadataResolver.HandleOf(instruction.Token));
            stackSize.Increment();
        }

        public void Visit(MethodCallInstruction instruction)
        {
            switch (instruction.Operation)
            {
                case MethodCallOperation.Virtual:
                    instructionEncoder.CallVirtual(metadataContainer.MetadataResolver.HandleOf(instruction.Method));
                    stackSize.Decrement(instruction.Method.Parameters.Count + 1);
                    break;
                case MethodCallOperation.Static:
                case MethodCallOperation.Jump:
                    instructionEncoder.Call(metadataContainer.MetadataResolver.HandleOf(instruction.Method));
                    stackSize.Decrement(instruction.Method.Parameters.Count);
                    break;
                default:
                    throw new UnhandledCase();
            }

            if (!instruction.Method.ReturnType.Equals(PlatformTypes.Void))
            {
                stackSize.Increment();
            }
        }

        public void Visit(IndirectMethodCallInstruction instruction)
        {
            var methodSignature = metadataContainer.MetadataResolver.HandleOf(instruction.Function);
            instructionEncoder.CallIndirect((SRM.StandaloneSignatureHandle) methodSignature);
            stackSize.Decrement(instruction.Function.Parameters.Count + 1);
            if (!instruction.Function.ReturnType.Equals(PlatformTypes.Void))
            {
                stackSize.Increment();
            }
        }

        public void Visit(CreateObjectInstruction instruction)
        {
            var method = metadataContainer.MetadataResolver.HandleOf(instruction.Constructor);
            instructionEncoder.OpCode(SRM.ILOpCode.Newobj);
            instructionEncoder.Token(method);
            stackSize.Decrement(instruction.Constructor.Parameters.Count);
            stackSize.Increment();
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
                stackSize.Decrement(instruction.Constructor.Parameters.Count);
                stackSize.Increment();
            }
        }

        public void Visit(LoadArrayElementInstruction instruction)
        {
            if (instruction.Method != null)
            {
                instructionEncoder.Call(metadataContainer.MetadataResolver.HandleOf(instruction.Method));
                stackSize.Decrement(instruction.Method.Parameters.Count);
                stackSize.Increment(); // FIXME creo que esto es porque en este caso se usa un metodo Get();
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

                stackSize.Decrement();
            }
        }

        public void Visit(StoreArrayElementInstruction instruction)
        {
            if (instruction.Method != null)
            {
                instructionEncoder.Call(metadataContainer.MetadataResolver.HandleOf(instruction.Method));
                stackSize.Decrement(instruction.Method.Parameters.Count);
                // FIXME y aca no iria increment porque es un Set()
            }
            else
            {
                if (instruction.Array.ElementsType.Equals(PlatformTypes.Int8))
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

                stackSize.Decrement(3);
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

        private class StackSize
        {
            private int currentStackSize;
            public int MaxStackSize { get; private set; }

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

            public void Decrement(int times = 1)
            {
                if (currentStackSize < times) throw new Exception("Stack size cannot be less than 0");
                currentStackSize -= times;
            }

            public void Clear()
            {
                currentStackSize = 0;
            }
        }
    }

    internal class UnhandledCase : Exception
    {
    }
}
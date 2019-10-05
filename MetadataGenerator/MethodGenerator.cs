using System;
using Model.Types;
using static MetadataGenerator.AttributesProvider;
using ECMA335 = System.Reflection.Metadata.Ecma335;
using SR = System.Reflection;
using SRM = System.Reflection.Metadata;

namespace MetadataGenerator
{
    public class MethodGenerator
    {
        private readonly ECMA335.MetadataBuilder metadata;
        private readonly ECMA335.MethodBodyStreamEncoder methodBodyStream;
        private readonly MethodSignatureGenerator methodSignatureGenerator;
        private readonly MethodBodyGenerator methodBodyGenerator;
        private readonly MethodParameterGenerator methodParameterGenerator;

        public MethodGenerator(ECMA335.MetadataBuilder metadata, TypeEncoder typeEncoder, ReferencesProvider referencesProvider)
        {
            this.metadata = metadata;
            methodBodyStream = new ECMA335.MethodBodyStreamEncoder(new SRM.BlobBuilder());
            methodParameterGenerator = new MethodParameterGenerator(metadata);
            methodSignatureGenerator = new MethodSignatureGenerator(typeEncoder);
            methodBodyGenerator = new MethodBodyGenerator(referencesProvider, methodSignatureGenerator, metadata);
        }

        public SRM.MethodDefinitionHandle Generate(MethodDefinition method)
        {
            var methodSignature = methodSignatureGenerator.GenerateSignatureOf(method);
            SRM.ParameterHandle? firstParameterHandle = null;
            foreach (var parameter in method.Parameters)
            {
                var parameterHandle = methodParameterGenerator.Generate(parameter);
                if (!firstParameterHandle.HasValue)
                {
                    firstParameterHandle = parameterHandle;
                }
            }

            // FIXME several addMethodBody variants with different arguments. codeSize, maxStacks, etc
            var methodBody = method.HasBody
                ? methodBodyStream.AddMethodBody(methodBodyGenerator.Generate(method.Body))
                : default;

            return metadata.AddMethodDefinition(
                attributes: GetMethodAttributesFor(method),
                implAttributes: SR.MethodImplAttributes.IL | SR.MethodImplAttributes.Managed, // FIXME what else?
                name: metadata.GetOrAddString(method.Name),
                signature: metadata.GetOrAddBlob(methodSignature),
                bodyOffset: methodBody,
                parameterList: firstParameterHandle ?? ECMA335.MetadataTokens.ParameterHandle(metadata.NextRowFor(ECMA335.TableIndex.Param)));
        }

        public SRM.BlobBuilder IlStream()
        {
            return methodBodyStream.Builder;
        }

        private class MethodSignatureGenerator
        {
            private readonly TypeEncoder typeEncoder;

            public MethodSignatureGenerator(TypeEncoder typeEncoder)
            {
                this.typeEncoder = typeEncoder;
            }

            public SRM.BlobBuilder GenerateSignatureOf(IMethodReference method)
            {
                var methodSignature = new SRM.BlobBuilder();
                new ECMA335.BlobEncoder(methodSignature)
                    .MethodSignature(isInstanceMethod: !method.IsStatic, genericParameterCount: method.GenericParameterCount)
                    .Parameters(
                        method.Parameters.Count,
                        returnType =>
                        {
                            if (method.ReturnType.Equals(PlatformTypes.Void))
                            {
                                returnType.Void();
                            }
                            else
                            {
                                // TODO isByRef param. ref in return type is not in the model
                                var encoder = returnType.Type();
                                typeEncoder.Encode(method.ReturnType, encoder);
                            }

                        },
                        parameters =>
                        {
                            foreach (var parameter in method.Parameters)
                            {
                                var encoder = parameters.AddParameter().Type(isByRef: parameter.Kind.IsOneOf(MethodParameterKind.Out, MethodParameterKind.Ref));
                                typeEncoder.Encode(parameter.Type, encoder);
                            }
                        });

                return methodSignature;
            }
        }

        private class MethodParameterGenerator
        {
            private readonly ECMA335.MetadataBuilder metadata;

            public MethodParameterGenerator(ECMA335.MetadataBuilder metadata)
            {
                this.metadata = metadata;
            }

            public SRM.ParameterHandle Generate(MethodParameter parameter) =>
                metadata.AddParameter(GetParameterAttributesFor(parameter), metadata.GetOrAddString(parameter.Name), parameter.Index);
        }

        #region Method Body
        // review all instructions of the ecma pdf
        // review overflow and other checks that instructions have
        private class MethodBodyGenerator
        {
            private readonly ReferencesProvider referencesProvider;
            private readonly MethodSignatureGenerator methodSignatureGenerator;
            private readonly ECMA335.MetadataBuilder metadata;

            public MethodBodyGenerator(ReferencesProvider referencesProvider, MethodSignatureGenerator methodSignatureGenerator, ECMA335.MetadataBuilder metadata)
            {
                this.referencesProvider = referencesProvider;
                this.methodSignatureGenerator = methodSignatureGenerator;
                this.metadata = metadata;
            }

            public ECMA335.InstructionEncoder Generate(MethodBody body)
            {
                var controlFlowBuilder = new ECMA335.ControlFlowBuilder();
                var instructionEncoder = new ECMA335.InstructionEncoder(new SRM.BlobBuilder(), controlFlowBuilder);

                /** Exception handling, uncomment once ial other instructions are generated correctly. If not, labels don't match (because operations are missing)
                var exceptionMapping = new Dictionary<string, IList<LabelHandle>>();
                LabelHandle addMapping(string label)
                {
                    var labelHandle = instructionsEncoder.DefineLabel();
                    if (exceptionMapping.TryGetValue(label, out var labelHandles))
                    {
                        labelHandles.Add(labelHandle);
                    }
                    else
                    {
                        exceptionMapping.Add(label, new List<LabelHandle> { labelHandle });
                    }
                    return labelHandle;
                }

                foreach (var protectedBlock in body.ExceptionInformation)
                {
                    var tryStart = addMapping(protectedBlock.Start);
                    var tryEnd = addMapping(protectedBlock.End);
                    var handlerStart = addMapping(protectedBlock.Handler.Start);
                    var handlerEnd = addMapping(protectedBlock.Handler.End);

                    switch (protectedBlock.Handler.Kind)
                    {
                        case Model.ExceptionHandlerBlockKind.Filter: // TODO 
                            break;
                        case Model.ExceptionHandlerBlockKind.Catch:
                            EntityHandle catchType = referencesProvider.TypeReferenceOf(PlatformTypes.Object); // FIXME
                            controlFlowBuilder.AddCatchRegion(tryStart, tryEnd, handlerStart, handlerEnd, catchType);
                            break;
                        case Model.ExceptionHandlerBlockKind.Fault: // TODO 
                            break;
                        case Model.ExceptionHandlerBlockKind.Finally: // TODO 
                            break;
                    }
                }*/

                foreach (var instruction in body.Instructions)
                {

                    /** Exception handling, uncomment once al other instructions are generated correctly. If not, labels don't match (because operations are missing)
                     * FIXME instruction has offset field. maybe that can be used with instructionsEncoder.offset instead of mapping labels (and using the extension method)
                    if (exceptionMapping.TryGetValue(instructionsEncoder.CurrentLabelString(), out var labels))
                    {
                        foreach (var label in labels)
                        {
                            instructionsEncoder.MarkLabel(label);
                        }
                    }
                    */


                    // FIXME visitor like in the model? or something better than this ifs?
                    if (instruction is Model.Bytecode.BasicInstruction basicInstruction)
                    {
                        switch (basicInstruction.Operation)
                        {
                            // TODO 
                            // check overflow for variants (ej: add, add_ovf, add_ovf_un)
                            // see all IlOpCode constants

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
                                break;
                            case Model.Bytecode.BasicOperation.Lt:
                                break;
                            case Model.Bytecode.BasicOperation.Gt:
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
                                // FIXME basicInstruction is missing type. Should be initObj type
                                // instructionEncoder.OpCode(ILOpCode.Initobj);
                                // instructionEncoder.Token(type)
                                break;
                            case Model.Bytecode.BasicOperation.CopyObject:
                                break;
                            case Model.Bytecode.BasicOperation.CopyBlock:
                                break;
                            case Model.Bytecode.BasicOperation.LoadArrayLength:
                                break;
                            case Model.Bytecode.BasicOperation.IndirectLoad:
                                break;
                            case Model.Bytecode.BasicOperation.LoadArrayElement:
                                break;
                            case Model.Bytecode.BasicOperation.LoadArrayElementAddress:
                                break;
                            case Model.Bytecode.BasicOperation.IndirectStore:
                                break;
                            case Model.Bytecode.BasicOperation.StoreArrayElement:
                                break;
                            case Model.Bytecode.BasicOperation.Breakpoint:
                                break;
                            case Model.Bytecode.BasicOperation.Return:
                                instructionEncoder.OpCode(SRM.ILOpCode.Ret);
                                break;
                        }
                    }
                    else if (instruction is Model.Bytecode.BranchInstruction branchInstruction)
                    {

                        switch (branchInstruction.Operation)
                        {
                            // TODO
                            case Model.Bytecode.BranchOperation.False:
                                break;
                            case Model.Bytecode.BranchOperation.True:
                                break;
                            case Model.Bytecode.BranchOperation.Eq:
                                break;
                            case Model.Bytecode.BranchOperation.Neq:
                                break;
                            case Model.Bytecode.BranchOperation.Lt:
                                break;
                            case Model.Bytecode.BranchOperation.Le:
                                break;
                            case Model.Bytecode.BranchOperation.Gt:
                                break;
                            case Model.Bytecode.BranchOperation.Ge:
                                break;
                            case Model.Bytecode.BranchOperation.Branch:
                                break;
                            case Model.Bytecode.BranchOperation.Leave:
                                break;
                        }
                    }
                    else if (instruction is Model.Bytecode.ConvertInstruction convertInstruction)
                    {
                        switch (convertInstruction.Operation)
                        {
                            case Model.Bytecode.ConvertOperation.Conv:
                                break;
                            case Model.Bytecode.ConvertOperation.Cast:
                                break;
                            case Model.Bytecode.ConvertOperation.Box:
                                instructionEncoder.OpCode(SRM.ILOpCode.Box);
                                // FIXME ConversionType is IType. It can be IBasicType, ArrayType or PointerType. 
                                instructionEncoder.Token(referencesProvider.TypeReferenceOf(convertInstruction.ConversionType as IBasicType));
                                break;
                            case Model.Bytecode.ConvertOperation.Unbox:
                                break;
                            case Model.Bytecode.ConvertOperation.UnboxPtr:
                                break;
                        }
                    }
                    else if (instruction is Model.Bytecode.MethodCallInstruction methodCallInstruction)
                    {
                        var methodSignature = methodSignatureGenerator.GenerateSignatureOf(methodCallInstruction.Method);
                        switch (methodCallInstruction.Operation)
                        {
                            case Model.Bytecode.MethodCallOperation.Virtual:
                                instructionEncoder.CallVirtual(referencesProvider.MethodReferenceOf(methodCallInstruction.Method, methodSignature));
                                break;
                            case Model.Bytecode.MethodCallOperation.Static:
                            case Model.Bytecode.MethodCallOperation.Jump:
                                instructionEncoder.Call(referencesProvider.MethodReferenceOf(methodCallInstruction.Method, methodSignature));
                                break;
                        }
                    }
                    else if (instruction is Model.Bytecode.LoadInstruction loadlInstruction)
                    {
                        switch (loadlInstruction.Operation)
                        {
                            case Model.Bytecode.LoadOperation.Address:
                                break;
                            case Model.Bytecode.LoadOperation.Value:
                                //FIXME TAC, CASTS?
                                if (loadlInstruction.Operand.Type.Equals(PlatformTypes.String))
                                {
                                    var value = (string)(loadlInstruction.Operand as Model.ThreeAddressCode.Values.Constant).Value;
                                    instructionEncoder.LoadString(metadata.GetOrAddUserString(value));
                                }

                                // TODO see ECMA ldc instruction. It says some cases should be follow by conv.i8 operations but the bytecode (original) does not have that
                                else if (loadlInstruction.Operand.Type.Equals(PlatformTypes.Int8) || loadlInstruction.Operand.Type.Equals(PlatformTypes.UInt8))
                                {
                                    var value = (int)(loadlInstruction.Operand as Model.ThreeAddressCode.Values.Constant).Value;
                                    instructionEncoder.OpCode(SRM.ILOpCode.Ldc_i4_s);
                                    instructionEncoder.Token(value);
                                }
                                else if (
                                    loadlInstruction.Operand.Type.Equals(PlatformTypes.Int16) ||
                                    loadlInstruction.Operand.Type.Equals(PlatformTypes.Int32) ||
                                    loadlInstruction.Operand.Type.Equals(PlatformTypes.UInt16) ||
                                    loadlInstruction.Operand.Type.Equals(PlatformTypes.UInt32))
                                {
                                    var value = (int)(loadlInstruction.Operand as Model.ThreeAddressCode.Values.Constant).Value;
                                    instructionEncoder.LoadConstantI4(value);
                                }
                                else if (loadlInstruction.Operand.Type.Equals(PlatformTypes.Int64) || loadlInstruction.Operand.Type.Equals(PlatformTypes.UInt64))
                                {
                                    var value = (long)(loadlInstruction.Operand as Model.ThreeAddressCode.Values.Constant).Value;
                                    instructionEncoder.LoadConstantI8(value);
                                }
                                else if (loadlInstruction.Operand.Type.Equals(PlatformTypes.Float32))
                                {
                                    var value = (float)(loadlInstruction.Operand as Model.ThreeAddressCode.Values.Constant).Value;
                                    instructionEncoder.LoadConstantR4(value);
                                }
                                else if (loadlInstruction.Operand.Type.Equals(PlatformTypes.Float64))
                                {
                                    var value = (double)(loadlInstruction.Operand as Model.ThreeAddressCode.Values.Constant).Value;
                                    instructionEncoder.LoadConstantR8(value);
                                }
                                break;
                            case Model.Bytecode.LoadOperation.Content:
                                break;
                        }
                    }
                    else if (instruction is Model.Bytecode.LoadFieldInstruction loadFieldInstruction) { }
                    else if (instruction is Model.Bytecode.LoadArrayElementInstruction loadArrayElementInstruction) { }
                    else if (instruction is Model.Bytecode.LoadMethodAddressInstruction loadMethodAdressInstruction) { }
                    else if (instruction is Model.Bytecode.CreateArrayInstruction createArrayInstruction)
                    {
                        //         var size = 1; // FIXME array size not in the model (ArrayType)
                        //       instructionEncoder.LoadConstantI4(size); // FIXME I4 = int, I8 = long. Could it be long?
                        //     instructionEncoder.OpCode(ILOpCode.Newarr);
                        // FIXME (cast). ElementsType could be Pointer or BasicType. MultiDimensional Arrays are handled by newObj insteado of newArr
                        //        instructionEncoder.Token(referencesProvider.TypeReferenceOf(createArrayInstruction.Type.ElementsType as IBasicType));
                    }
                    else if (instruction is Model.Bytecode.CreateObjectInstruction createObjectInstruction) { }
                    else if (instruction is Model.Bytecode.LoadMethodAddressInstruction loadMethodAddressInstruction) { }
                    else if (instruction is Model.Bytecode.StoreInstruction storeInstruction) { }
                    else if (instruction is Model.Bytecode.StoreFieldInstruction storeFieldInstruction) { }
                    else if (instruction is Model.Bytecode.SwitchInstruction switchInstruction) { }
                    else if (instruction is Model.Bytecode.SizeofInstruction sizeofInstruction) { }
                    else if (instruction is Model.Bytecode.LoadTokenInstruction loadTokenInstruction) { }
                    else if (instruction is Model.Bytecode.IndirectMethodCallInstruction indirectMethodCallInstruction) { }
                    else if (instruction is Model.Bytecode.StoreArrayElementInstruction storeArrayElementInstruction) { }
                    else throw new Exception("instruction type not handled");

                }

                return instructionEncoder;
            }
        }
        #endregion
    }
}

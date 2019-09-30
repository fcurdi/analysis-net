using System;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Model.Types;

namespace MetadataGenerator
{
    public class MethodGenerator
    {
        private readonly MetadataBuilder metadata;
        private readonly MethodBodyStreamEncoder methodBodyStream;
        private readonly MethodSignatureGenerator methodSignatureGenerator;
        private readonly MethodBodyGenerator methodBodyGenerator;
        private readonly MethodParameterGenerator methodParameterGenerator;

        public MethodGenerator(MetadataBuilder metadata, TypeEncoder typeEncoder, ReferencesProvider referencesProvider)
        {
            this.metadata = metadata;
            methodBodyStream = new MethodBodyStreamEncoder(new BlobBuilder());
            methodParameterGenerator = new MethodParameterGenerator(metadata);
            methodSignatureGenerator = new MethodSignatureGenerator(typeEncoder);
            methodBodyGenerator = new MethodBodyGenerator(referencesProvider, methodSignatureGenerator);
        }

        public MethodDefinitionHandle Generate(Model.Types.MethodDefinition method)
        {

            var methodSignature = methodSignatureGenerator.Generate(method);

            ParameterHandle? firstParameterHandle = null;
            foreach (var parameter in method.Parameters)
            {
                var parameterHandle = methodParameterGenerator.Generate(parameter);
                if (!firstParameterHandle.HasValue)
                {
                    firstParameterHandle = parameterHandle;
                }
            }

            // FIXME several addMethodBody variants with different arguments
            var methodBody = method.HasBody
                ? methodBodyStream.AddMethodBody(methodBodyGenerator.Generate(method.Body))
                : default(int);

            return metadata.AddMethodDefinition(
                attributes: AttributesProvider.GetMethodAttributesFor(method),
                implAttributes: MethodImplAttributes.IL | MethodImplAttributes.Managed, //FIXME
                name: metadata.GetOrAddString(method.Name),
                signature: metadata.GetOrAddBlob(methodSignature),
                bodyOffset: methodBody,
                parameterList: firstParameterHandle ?? MetadataTokens.ParameterHandle(metadata.NextRowFor(TableIndex.Param)));
        }

        public BlobBuilder IlStream()
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

            public BlobBuilder Generate(IMethodReference method)
            {
                var methodSignature = new BlobBuilder();
                new BlobEncoder(methodSignature)
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
                                var encoder = returnType.Type(); // FIXME pass isByRef param. ref return type is not in the model
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
            private readonly MetadataBuilder metadata;

            public MethodParameterGenerator(MetadataBuilder metadata)
            {
                this.metadata = metadata;
            }

            public ParameterHandle Generate(MethodParameter methodParameter)
            {
                return metadata.AddParameter(
                    AttributesProvider.GetParameterAttributesFor(methodParameter),
                    metadata.GetOrAddString(methodParameter.Name),
                    methodParameter.Index);
            }

        }

        private class MethodBodyGenerator
        {
            private readonly ReferencesProvider referencesProvider;
            private readonly MethodSignatureGenerator methodSignatureGenerator;

            public MethodBodyGenerator(ReferencesProvider referencesProvider, MethodSignatureGenerator methodSignatureGenerator)
            {
                this.referencesProvider = referencesProvider;
                this.methodSignatureGenerator = methodSignatureGenerator;
            }

            public InstructionEncoder Generate(Model.Types.MethodBody body)
            {
                var controlFlowBuilder = new ControlFlowBuilder();
                var instructionEncoder = new InstructionEncoder(new BlobBuilder(), controlFlowBuilder);

                /** Exception handling, uncomment once al other instructions are generated correctly. If not, labels don't match (because operations are missing)
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
                                instructionEncoder.OpCode(ILOpCode.Nop);
                                break;
                            case Model.Bytecode.BasicOperation.Add:
                                instructionEncoder.OpCode(ILOpCode.Add);
                                break;
                            case Model.Bytecode.BasicOperation.Sub:
                                instructionEncoder.OpCode(ILOpCode.Sub);
                                break;
                            case Model.Bytecode.BasicOperation.Mul:
                                instructionEncoder.OpCode(ILOpCode.Mul);
                                break;
                            case Model.Bytecode.BasicOperation.Div:
                                instructionEncoder.OpCode(ILOpCode.Div);
                                break;
                            case Model.Bytecode.BasicOperation.Rem:
                                instructionEncoder.OpCode(ILOpCode.Rem);
                                break;
                            case Model.Bytecode.BasicOperation.And:
                                instructionEncoder.OpCode(ILOpCode.And);
                                break;
                            case Model.Bytecode.BasicOperation.Or:
                                instructionEncoder.OpCode(ILOpCode.Or);
                                break;
                            case Model.Bytecode.BasicOperation.Xor:
                                instructionEncoder.OpCode(ILOpCode.Xor);
                                break;
                            case Model.Bytecode.BasicOperation.Shl:
                                instructionEncoder.OpCode(ILOpCode.Shl);
                                break;
                            case Model.Bytecode.BasicOperation.Shr:
                                instructionEncoder.OpCode(ILOpCode.Shr);
                                break;
                            case Model.Bytecode.BasicOperation.Eq:
                                break;
                            case Model.Bytecode.BasicOperation.Lt:
                                break;
                            case Model.Bytecode.BasicOperation.Gt:
                                break;
                            case Model.Bytecode.BasicOperation.Throw:
                                instructionEncoder.OpCode(ILOpCode.Throw);
                                break;
                            case Model.Bytecode.BasicOperation.Rethrow:
                                instructionEncoder.OpCode(ILOpCode.Rethrow);
                                break;
                            case Model.Bytecode.BasicOperation.Not:
                                instructionEncoder.OpCode(ILOpCode.Not);
                                break;
                            case Model.Bytecode.BasicOperation.Neg:
                                instructionEncoder.OpCode(ILOpCode.Neg);
                                break;
                            case Model.Bytecode.BasicOperation.Pop:
                                instructionEncoder.OpCode(ILOpCode.Pop);
                                break;
                            case Model.Bytecode.BasicOperation.Dup:
                                instructionEncoder.OpCode(ILOpCode.Dup);
                                break;
                            case Model.Bytecode.BasicOperation.EndFinally:
                                instructionEncoder.OpCode(ILOpCode.Endfinally);
                                break;
                            case Model.Bytecode.BasicOperation.EndFilter:
                                instructionEncoder.OpCode(ILOpCode.Endfilter);
                                break;
                            case Model.Bytecode.BasicOperation.LocalAllocation:
                                instructionEncoder.OpCode(ILOpCode.Localloc);
                                break;
                            case Model.Bytecode.BasicOperation.InitBlock:
                                instructionEncoder.OpCode(ILOpCode.Initblk);
                                break;
                            case Model.Bytecode.BasicOperation.InitObject:
                                instructionEncoder.OpCode(ILOpCode.Initobj);
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
                                instructionEncoder.OpCode(ILOpCode.Ret);
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
                    }
                    else if (instruction is Model.Bytecode.MethodCallInstruction methodCallInstruction)
                    {
                        var methodSignature = methodSignatureGenerator.Generate(methodCallInstruction.Method);
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
                        var size = 1; // FIXME array size not in the model (ArrayType)
                        instructionEncoder.LoadConstantI4(size); // FIXME I4 = int, I8 = long. Could it be long?
                        instructionEncoder.OpCode(ILOpCode.Newarr);
                        // FIXME (cast). ElementsType could be Pointer or BasicType. MultiDimensional Arrays are handled by newObj insteado of newArr
                        instructionEncoder.Token(referencesProvider.TypeReferenceOf(createArrayInstruction.Type.ElementsType as IBasicType));
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
    }

}
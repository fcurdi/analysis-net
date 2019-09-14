using System.Collections.Generic;
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
        private readonly MethodReferences methodReferencesAndSignatures;

        public MethodGenerator(MetadataBuilder metadata, TypeEncoder typeEncoder)
        {
            this.metadata = metadata;
            methodBodyStream = new MethodBodyStreamEncoder(new BlobBuilder());
            methodParameterGenerator = new MethodParameterGenerator(metadata);
            methodSignatureGenerator = new MethodSignatureGenerator(typeEncoder);
            methodReferencesAndSignatures = new MethodReferences(metadata, typeEncoder, methodSignatureGenerator);
            methodBodyGenerator = new MethodBodyGenerator(methodReferencesAndSignatures);
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
            private readonly MethodReferences methodReferencesAndSignatures;

            public MethodBodyGenerator(MethodReferences methodReferencesAndSignatures)
            {
                this.methodReferencesAndSignatures = methodReferencesAndSignatures;
            }

            public InstructionEncoder Generate(Model.Types.MethodBody body)
            {
                var encoder = new InstructionEncoder(new BlobBuilder());

                foreach (var instruction in body.Instructions)
                {
                    // TODO implement all of them

                    if (instruction is Model.Bytecode.BasicInstruction basicInstruction)
                    {
                        switch (basicInstruction.Operation)
                        {
                            // TODO 
                            // check overflow for variants (ej: add, add_ovf, add_ovf_un)
                            // see all IlOpCode constants

                            case Model.Bytecode.BasicOperation.Nop:
                                encoder.OpCode(ILOpCode.Nop);
                                break;
                            case Model.Bytecode.BasicOperation.Add:
                                encoder.OpCode(ILOpCode.Add);
                                break;
                            case Model.Bytecode.BasicOperation.Sub:
                                encoder.OpCode(ILOpCode.Sub);
                                break;
                            case Model.Bytecode.BasicOperation.Mul:
                                encoder.OpCode(ILOpCode.Mul);
                                break;
                            case Model.Bytecode.BasicOperation.Div:
                                encoder.OpCode(ILOpCode.Div);
                                break;
                            case Model.Bytecode.BasicOperation.Rem:
                                encoder.OpCode(ILOpCode.Rem);
                                break;
                            case Model.Bytecode.BasicOperation.And:
                                encoder.OpCode(ILOpCode.And);
                                break;
                            case Model.Bytecode.BasicOperation.Or:
                                encoder.OpCode(ILOpCode.Or);
                                break;
                            case Model.Bytecode.BasicOperation.Xor:
                                encoder.OpCode(ILOpCode.Xor);
                                break;
                            case Model.Bytecode.BasicOperation.Shl:
                                encoder.OpCode(ILOpCode.Shl);
                                break;
                            case Model.Bytecode.BasicOperation.Shr:
                                encoder.OpCode(ILOpCode.Shr);
                                break;
                            case Model.Bytecode.BasicOperation.Eq:
                                break;
                            case Model.Bytecode.BasicOperation.Lt:
                                break;
                            case Model.Bytecode.BasicOperation.Gt:
                                break;
                            case Model.Bytecode.BasicOperation.Throw:
                                encoder.OpCode(ILOpCode.Throw);
                                break;
                            case Model.Bytecode.BasicOperation.Rethrow:
                                encoder.OpCode(ILOpCode.Rethrow);
                                break;
                            case Model.Bytecode.BasicOperation.Not:
                                encoder.OpCode(ILOpCode.Not);
                                break;
                            case Model.Bytecode.BasicOperation.Neg:
                                encoder.OpCode(ILOpCode.Neg);
                                break;
                            case Model.Bytecode.BasicOperation.Pop:
                                break;
                            case Model.Bytecode.BasicOperation.Dup:
                                break;
                            case Model.Bytecode.BasicOperation.EndFinally:
                                encoder.OpCode(ILOpCode.Endfinally);
                                break;
                            case Model.Bytecode.BasicOperation.EndFilter:
                                encoder.OpCode(ILOpCode.Endfilter);
                                break;
                            case Model.Bytecode.BasicOperation.LocalAllocation:
                                encoder.OpCode(ILOpCode.Localloc);
                                break;
                            case Model.Bytecode.BasicOperation.InitBlock:
                                break;
                            case Model.Bytecode.BasicOperation.InitObject:
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
                                encoder.OpCode(ILOpCode.Ret);
                                break;
                        }
                    }
                    else if (instruction is Model.Bytecode.BranchInstruction branchInstruction)
                    {
                    }
                    else if (instruction is Model.Bytecode.ConvertInstruction convertInstruction)
                    {

                    }
                    else if (instruction is Model.Bytecode.MethodCallInstruction methodCallInstruction)
                    {
                        switch (methodCallInstruction.Operation)
                        {
                            case Model.Bytecode.MethodCallOperation.Virtual:
                                encoder.CallVirtual(methodReferencesAndSignatures.MethodReferenceOf(methodCallInstruction.Method));
                                break;
                            case Model.Bytecode.MethodCallOperation.Static:
                            case Model.Bytecode.MethodCallOperation.Jump:
                                encoder.Call(methodReferencesAndSignatures.MethodReferenceOf(methodCallInstruction.Method));
                                break;
                        }
                    }
                    else if (instruction is Model.Bytecode.LoadInstruction loadlInstruction)
                    {

                        // TODO implement

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

                }
                return encoder;
            }
        }

        private class MethodReferences
        {
            private readonly IDictionary<string, MemberReferenceHandle> methodReferences = new Dictionary<string, MemberReferenceHandle>();
            private MetadataBuilder metadata;
            private readonly TypeEncoder typeEncoder;
            private readonly MethodSignatureGenerator methodSignatureGenerator;

            public MethodReferences(MetadataBuilder metadata, TypeEncoder typeEncoder, MethodSignatureGenerator methodSignatureGenerator)
            {
                this.metadata = metadata;
                this.typeEncoder = typeEncoder;
                this.methodSignatureGenerator = methodSignatureGenerator;
            }

            public MemberReferenceHandle MethodReferenceOf(IMethodReference method)
            {
                MemberReferenceHandle memberReferenceHandle;
                var key = $"{method.ContainingType.ContainingAssembly.Name}.{method.ContainingType.ContainingNamespace}.{method.ContainingType.Name}.{method.Name}";
                if (methodReferences.TryGetValue(key, out var value))
                {
                    memberReferenceHandle = value;
                }
                else
                {
                    var methodSignature = methodSignatureGenerator.Generate(method);
                    memberReferenceHandle = metadata.AddMemberReference(
                            parent: typeEncoder.typeReferences.TypeReferenceOf(method.ContainingType), // FIXME cualquiera esto
                            name: metadata.GetOrAddString(method.Name),
                            signature: metadata.GetOrAddBlob(methodSignature));
                    methodReferences.Add(key, memberReferenceHandle);
                }



                return memberReferenceHandle;
            }
        }

    }

}
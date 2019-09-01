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
        private int nextOffset;
        private readonly TypeEncoder typeEncoder;
        private readonly MethodParameterGenerator methodParameterGenerator;

        public MethodGenerator(MetadataBuilder metadata, TypeEncoder typeEncoder)
        {
            this.metadata = metadata;
            this.methodBodyStream = new MethodBodyStreamEncoder(new BlobBuilder());
            nextOffset = 1;
            this.typeEncoder = typeEncoder;
            methodParameterGenerator = new MethodParameterGenerator(metadata);
        }

        public MethodDefinitionHandle Generate(Model.Types.MethodDefinition method)
        {
            ParameterHandle? firstParameterHandle = null;
            var methodSignature = new BlobBuilder();
            new BlobEncoder(methodSignature)
                .MethodSignature(isInstanceMethod: !method.IsStatic, genericParameterCount: method.GenericParameters.Count)
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
                            var parameterHandle = methodParameterGenerator.Generate(parameter);
                            if (!firstParameterHandle.HasValue)
                            {
                                firstParameterHandle = parameterHandle;
                            }
                            var encoder = parameters.AddParameter().Type(isByRef: parameter.Kind.IsOneOf(MethodParameterKind.Out, MethodParameterKind.Ref));
                            typeEncoder.Encode(parameter.Type, encoder);
                        }
                    });

            var instructions = new InstructionEncoder(new BlobBuilder());

            // TODO real body
            instructions.OpCode(ILOpCode.Nop);
            instructions.OpCode(ILOpCode.Ret);

            nextOffset++;

            return metadata.AddMethodDefinition(
                attributes: AttributesProvider.GetMethodAttributesFor(method),
                implAttributes: MethodImplAttributes.IL | MethodImplAttributes.Managed, //FIXME
                name: metadata.GetOrAddString(method.Name),
                signature: metadata.GetOrAddBlob(methodSignature),
                bodyOffset: methodBodyStream.AddMethodBody(instructions),
                parameterList: firstParameterHandle ?? methodParameterGenerator.NextParameterHandle());
        }

        public BlobBuilder IlStream()
        {
            return methodBodyStream.Builder;
        }

        public MethodDefinitionHandle NextMethodHandle()
        {
            return MetadataTokens.MethodDefinitionHandle(nextOffset);
        }


        private class MethodParameterGenerator
        {
            private readonly MetadataBuilder metadata;
            private int nextOffset;

            public MethodParameterGenerator(MetadataBuilder metadata)
            {
                this.metadata = metadata;
                nextOffset = 1;
            }

            public ParameterHandle Generate(Model.Types.MethodParameter methodParameter)
            {
                nextOffset++;
                return metadata.AddParameter(
                    AttributesProvider.GetParameterAttributesFor(methodParameter),
                    metadata.GetOrAddString(methodParameter.Name),
                    methodParameter.Index);
            }

            public ParameterHandle NextParameterHandle()
            {
                return MetadataTokens.ParameterHandle(nextOffset);
            }


        }

    }

}

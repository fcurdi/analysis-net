using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace MetadataGenerator
{
    public class MethodGenerator
    {
        private readonly MetadataBuilder metadata;
        private readonly MethodBodyStreamEncoder methodBodyStream;
        private int nextOffset;
        private readonly TypeEncoder typeEncoder;

        public MethodGenerator(MetadataBuilder metadata, TypeEncoder typeEncoder)
        {
            this.metadata = metadata;
            this.methodBodyStream = new MethodBodyStreamEncoder(new BlobBuilder());
            this.nextOffset = 1;
            this.typeEncoder = typeEncoder;
        }

        public MethodDefinitionHandle Generate(Model.Types.MethodDefinition method)
        {
            var methodSignature = new BlobBuilder();
            new BlobEncoder(methodSignature)
                .MethodSignature(isInstanceMethod: !method.IsStatic)
                .Parameters(
                    method.Parameters.Count,
                    returnType =>
                    {
                        if (method.ReturnType.Equals(Model.Types.PlatformTypes.Void))
                        {
                            returnType.Void();
                        }
                        else
                        {
                            typeEncoder.Encode(method.ReturnType, returnType.Type());
                        }

                    },
                    parameters =>
                    {
                        foreach (var parameter in method.Parameters)
                        {
                            typeEncoder.Encode(parameter.Type, parameters.AddParameter().Type());
                        }
                    });
            var instructions = new InstructionEncoder(new BlobBuilder());

            //TODO: real body
            instructions.OpCode(ILOpCode.Nop);
            instructions.OpCode(ILOpCode.Ret);

            nextOffset++;

            return metadata.AddMethodDefinition(
                attributes: AttributesProvider.GetAttributesFor(method),
                implAttributes: MethodImplAttributes.IL | MethodImplAttributes.Managed, //FIXME
                name: metadata.GetOrAddString(method.Name),
                signature: metadata.GetOrAddBlob(methodSignature),
                bodyOffset: methodBodyStream.AddMethodBody(instructions),
                parameterList: default(ParameterHandle));
        }

        public BlobBuilder IlStream()
        {
            return methodBodyStream.Builder;
        }

        public MethodDefinitionHandle NextMethodHandle()
        {
            return MetadataTokens.MethodDefinitionHandle(nextOffset);
        }


    }
}

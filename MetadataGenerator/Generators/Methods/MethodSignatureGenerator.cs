using Model.Types;
using ECMA335 = System.Reflection.Metadata.Ecma335;
using SRM = System.Reflection.Metadata;

namespace MetadataGenerator.Generators.Methods
{
    class MethodSignatureGenerator
    {
        private readonly MetadataContainer metadataContainer;

        public MethodSignatureGenerator(MetadataContainer metadataContainer)
        {
            this.metadataContainer = metadataContainer;
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
                            metadataContainer.Encode(method.ReturnType, encoder);
                        }

                    },
                    parameters =>
                    {
                        foreach (var parameter in method.Parameters)
                        {
                            bool isByRef = parameter.Kind.IsOneOf(MethodParameterKind.Out, MethodParameterKind.Ref);
                            var type = isByRef ? (parameter.Type as PointerType).TargetType : parameter.Type;
                            var encoder = parameters.AddParameter().Type(isByRef);
                            metadataContainer.Encode(type, encoder);
                        }
                    });
            return methodSignature;
        }
    }
}

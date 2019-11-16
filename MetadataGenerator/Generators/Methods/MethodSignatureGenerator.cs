using System.Collections.Generic;
using MetadataGenerator.Metadata;
using Model.Types;
using ECMA335 = System.Reflection.Metadata.Ecma335;
using SRM = System.Reflection.Metadata;

namespace MetadataGenerator.Generators.Methods
{
    internal class MethodSignatureGenerator
    {
        private readonly MetadataContainer metadataContainer;

        public MethodSignatureGenerator(MetadataContainer metadataContainer)
        {
            this.metadataContainer = metadataContainer;
        }

        public SRM.BlobBuilder GenerateSignatureOf(IMethodReference method) =>
            GenerateMethodSignature(method.IsStatic, method.GenericParameterCount, method.Parameters, method.ReturnType);

        public SRM.BlobBuilder GenerateSignatureOf(FunctionPointerType method) =>
            GenerateMethodSignature(method.IsStatic, 0, method.Parameters, method.ReturnType);
        // FIXME 0 because FunctionPointerType does not have that property (there's a fixme in that class)

        private SRM.BlobBuilder GenerateMethodSignature(
            bool isStatic,
            int genericParameterCount,
            IList<IMethodParameterReference> parameters,
            IType returnType)
        {
            var methodSignature = new SRM.BlobBuilder();
            new ECMA335.BlobEncoder(methodSignature)
                .MethodSignature(isInstanceMethod: !isStatic, genericParameterCount: genericParameterCount)
                .Parameters(
                    parameters.Count,
                    returnTypeEncoder =>
                    {
                        if (returnType.Equals(PlatformTypes.Void))
                        {
                            returnTypeEncoder.Void();
                        }
                        else
                        {
                            // TODO isByRef param. ref in return type is not in the model
                            var encoder = returnTypeEncoder.Type();
                            metadataContainer.Encode(returnType, encoder);
                        }
                    },
                    parametersEncoder =>
                    {
                        foreach (var parameter in parameters)
                        {
                            var isByRef = parameter.Kind.IsOneOf(MethodParameterKind.Out, MethodParameterKind.Ref);
                            var type = isByRef ? (parameter.Type as PointerType).TargetType : parameter.Type;
                            var encoder = parametersEncoder.AddParameter().Type(isByRef);
                            metadataContainer.Encode(type, encoder);
                        }
                    });
            return methodSignature;
        }
    }
}
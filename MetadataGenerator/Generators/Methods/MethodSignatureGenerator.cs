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

        public SRM.BlobBuilder GenerateSignatureOf(IMethodReference method)
        {
            if (method.IsGenericInstantiation())
            {
                var signature = new SRM.BlobBuilder();
                var encoder = new ECMA335.BlobEncoder(signature).MethodSpecificationSignature(method.GenericArguments.Count);
                foreach (var genericArg in method.GenericArguments)
                {
                    metadataContainer.MetadataResolver.Encode(genericArg, encoder.AddArgument());
                }

                return signature;
            }
            else
            {
                return GenerateMethodSignature(method.IsStatic, method.GenericParameterCount, method.Parameters, method.ReturnType);
            }
        }

        // 0 because FunctionPointerType does not have that property (there's a comment in that class)
        public SRM.BlobBuilder GenerateSignatureOf(FunctionPointerType method) =>
            GenerateMethodSignature(method.IsStatic, 0, method.Parameters, method.ReturnType);

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
                            metadataContainer.MetadataResolver.Encode(returnType, encoder);
                        }
                    },
                    parametersEncoder =>
                    {
                        foreach (var parameter in parameters)
                        {
                            var isByRef = false;
                            var type = parameter.Type;
                            if (parameter.Type is PointerType pointerType)
                            {
                                isByRef = pointerType.Managed;
                                type = pointerType.TargetType;
                            }

                            var encoder = parametersEncoder.AddParameter().Type(isByRef);
                            metadataContainer.MetadataResolver.Encode(type, encoder);
                        }
                    });
            return methodSignature;
        }
    }
}
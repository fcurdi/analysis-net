using System.Collections.Generic;
using MetadataGenerator.Generation.Types;
using Model.Types;
using ECMA335 = System.Reflection.Metadata.Ecma335;
using SRM = System.Reflection.Metadata;

namespace MetadataGenerator.Generation.Methods
{
    internal class MethodSignatureEncoder
    {
        private readonly TypeEncoder typeEncoder;

        public MethodSignatureEncoder(TypeEncoder typeEncoder)
        {
            this.typeEncoder = typeEncoder;
        }

        public SRM.BlobBuilder EncodeSignatureOf(IMethodReference method)
        {
            if (method.IsGenericInstantiation())
            {
                var signature = new SRM.BlobBuilder();
                var encoder = new ECMA335.BlobEncoder(signature).MethodSpecificationSignature(method.GenericArguments.Count);
                foreach (var genericArg in method.GenericArguments)
                {
                    typeEncoder.Encode(genericArg, encoder.AddArgument());
                }

                return signature;
            }
            else
            {
                return EncodeSignature(method.IsStatic, method.GenericParameterCount, method.Parameters, method.ReturnType);
            }
        }

        // 0 because FunctionPointerType does not have that property
        public SRM.BlobBuilder EncodeSignatureOf(FunctionPointerType method) =>
            EncodeSignature(method.IsStatic, 0, method.Parameters, method.ReturnType);

        private SRM.BlobBuilder EncodeSignature(
            bool isStatic,
            int genericParameterCount,
            IList<IMethodParameterReference> parameters,
            IType returnType)
        {
            var signature = new SRM.BlobBuilder();
            new ECMA335.BlobEncoder(signature)
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
                            var encoder = returnTypeEncoder.Type();
                            typeEncoder.Encode(returnType, encoder);
                        }
                    },
                    parametersEncoder =>
                    {
                        foreach (var parameter in parameters)
                        {
                            var isByRef = false;
                            var type = parameter.Type;
                            if (type is PointerType pointerType)
                            {
                                isByRef = pointerType.Managed;
                                type = pointerType.TargetType;
                            }

                            var encoder = parametersEncoder.AddParameter().Type(isByRef);
                            typeEncoder.Encode(type, encoder);
                        }
                    });

            return signature;
        }
    }
}
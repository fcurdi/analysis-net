using Model.Types;
using ECMA335 = System.Reflection.Metadata.Ecma335;
using SRM = System.Reflection.Metadata;

namespace MetadataGenerator.Generation.Properties
{
    internal class PropertySignatureEncoder
    {
        private readonly HandleResolver _handleResolver;

        public PropertySignatureEncoder(HandleResolver handleResolver)
        {
            this._handleResolver = handleResolver;
        }

        public SRM.BlobBuilder EncodeSignatureOf(PropertyDefinition property)
        {
            var signature = new SRM.BlobBuilder();
            new ECMA335.BlobEncoder(signature)
                .PropertySignature(isInstanceProperty: !property.IsStatic)
                .Parameters(
                    parameterCount: 0,
                    returnType: returnTypeEncoder => _handleResolver.Encode(property.PropertyType, returnTypeEncoder.Type()),
                    parameters: parametersEncoder => { });
            return signature;
        }
    }
}
using Model.Types;
using ECMA335 = System.Reflection.Metadata.Ecma335;
using SRM = System.Reflection.Metadata;

namespace MetadataGenerator.Generation.Types
{
    internal class TypeSignatureEncoder
    {
        private readonly HandleResolver _handleResolver;

        public TypeSignatureEncoder(HandleResolver handleResolver)
        {
            this._handleResolver = handleResolver;
        }

        public SRM.BlobBuilder EncodeSignatureOf(IType type)
        {
            var signature = new SRM.BlobBuilder();
            _handleResolver.Encode(type, new ECMA335.BlobEncoder(signature).TypeSpecificationSignature());
            return signature;
        }
    }
}
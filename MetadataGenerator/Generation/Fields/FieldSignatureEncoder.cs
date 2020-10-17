using Model.Types;
using ECMA335 = System.Reflection.Metadata.Ecma335;
using SRM = System.Reflection.Metadata;

namespace MetadataGenerator.Generation.Fields
{
    internal class FieldSignatureEncoder
    {
        private readonly HandleResolver _handleResolver;

        public FieldSignatureEncoder(HandleResolver handleResolver)
        {
            this._handleResolver = handleResolver;
        }

        public SRM.BlobBuilder EncodeSignatureOf(IFieldReference field)
        {
            var fieldSignature = new SRM.BlobBuilder();
            _handleResolver.Encode(field.Type, new ECMA335.BlobEncoder(fieldSignature).FieldSignature());
            return fieldSignature;
        }
    }
}
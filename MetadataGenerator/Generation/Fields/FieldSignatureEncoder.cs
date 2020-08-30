using Model.Types;
using ECMA335 = System.Reflection.Metadata.Ecma335;
using SRM = System.Reflection.Metadata;

namespace MetadataGenerator.Generation.Fields
{
    internal class FieldSignatureEncoder
    {
        private readonly MetadataResolver metadataResolver;

        public FieldSignatureEncoder(MetadataResolver metadataResolver)
        {
            this.metadataResolver = metadataResolver;
        }

        public SRM.BlobBuilder EncodeSignatureOf(IFieldReference field)
        {
            var fieldSignature = new SRM.BlobBuilder();
            metadataResolver.Encode(field.Type, new ECMA335.BlobEncoder(fieldSignature).FieldSignature());
            return fieldSignature;
        }
    }
}
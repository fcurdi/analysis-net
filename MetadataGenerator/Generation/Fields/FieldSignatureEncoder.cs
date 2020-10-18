using MetadataGenerator.Generation.Types;
using Model.Types;
using ECMA335 = System.Reflection.Metadata.Ecma335;
using SRM = System.Reflection.Metadata;

namespace MetadataGenerator.Generation.Fields
{
    internal class FieldSignatureEncoder
    {
        private readonly TypeEncoder typeEncoder;

        public FieldSignatureEncoder(TypeEncoder typeEncoder)
        {
            this.typeEncoder = typeEncoder;
        }

        public SRM.BlobBuilder EncodeSignatureOf(IFieldReference field)
        {
            var fieldSignature = new SRM.BlobBuilder();
            typeEncoder.Encode(field.Type, new ECMA335.BlobEncoder(fieldSignature).FieldSignature());
            return fieldSignature;
        }
    }
}
using Model.Types;
using ECMA335 = System.Reflection.Metadata.Ecma335;
using SRM = System.Reflection.Metadata;

namespace MetadataGenerator.Generators.Fields
{
    internal class FieldSignatureGenerator
    {
        private readonly MetadataContainer metadataContainer;

        public FieldSignatureGenerator(MetadataContainer metadataContainer)
        {
            this.metadataContainer = metadataContainer;
        }

        public SRM.BlobBuilder GenerateSignatureOf(IFieldReference field)
        {
            var fieldSignature = new SRM.BlobBuilder();
            metadataContainer.MetadataResolver.Encode(field.Type, new ECMA335.BlobEncoder(fieldSignature).FieldSignature());
            return fieldSignature;
        }
    }
}
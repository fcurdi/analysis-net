using Model.Types;
using static MetadataGenerator.AttributesProvider;
using ECMA335 = System.Reflection.Metadata.Ecma335;
using SRM = System.Reflection.Metadata;

namespace MetadataGenerator
{
    class FieldGenerator
    {
        private readonly MetadataContainer metadataContainer;

        public FieldGenerator(MetadataContainer metadataContainer)
        {
            this.metadataContainer = metadataContainer;
        }

        public SRM.FieldDefinitionHandle Generate(FieldDefinition field)
        {
            var fieldSignature = new SRM.BlobBuilder();
            metadataContainer.Encode(field.Type, new ECMA335.BlobEncoder(fieldSignature).FieldSignature());
            var fieldDefinitionHandle = metadataContainer.metadataBuilder.AddFieldDefinition(
                    attributes: GetFieldAttributesFor(field),
                    name: metadataContainer.metadataBuilder.GetOrAddString(field.Name),
                    signature: metadataContainer.metadataBuilder.GetOrAddBlob(fieldSignature));

            if (field.Value != null)
            {
                metadataContainer.metadataBuilder.AddConstant(fieldDefinitionHandle, field.Value.Value);
            }

            return fieldDefinitionHandle;
        }
    }
}
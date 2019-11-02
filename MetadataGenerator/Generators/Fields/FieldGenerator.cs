using MetadataGenerator.Generators.Fields;
using Model.Types;
using static MetadataGenerator.AttributesProvider;
using SRM = System.Reflection.Metadata;
namespace MetadataGenerator
{
    class FieldGenerator
    {
        private readonly MetadataContainer metadataContainer;
        private readonly FieldSignatureGenerator fieldSignatureGenerator;

        public FieldGenerator(MetadataContainer metadataContainer)
        {
            this.metadataContainer = metadataContainer;
            fieldSignatureGenerator = new FieldSignatureGenerator(metadataContainer);
        }

        public SRM.FieldDefinitionHandle Generate(FieldDefinition field)
        {
            var fieldSignature = fieldSignatureGenerator.GenerateSignatureOf(field);
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
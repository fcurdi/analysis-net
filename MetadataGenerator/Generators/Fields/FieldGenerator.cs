using System.Reflection.PortableExecutable;
using MetadataGenerator.Metadata;
using Model.Types;
using static MetadataGenerator.Metadata.AttributesProvider;
using SRM = System.Reflection.Metadata;

namespace MetadataGenerator.Generators.Fields
{
    internal class FieldGenerator
    {
        private readonly MetadataContainer metadataContainer;
        private readonly FieldSignatureGenerator fieldSignatureGenerator;
        private readonly CustomAttributeGenerator customAttributeGenerator;

        public FieldGenerator(MetadataContainer metadataContainer)
        {
            this.metadataContainer = metadataContainer;
            fieldSignatureGenerator = new FieldSignatureGenerator(metadataContainer);
            customAttributeGenerator = new CustomAttributeGenerator(metadataContainer);
        }

        public SRM.FieldDefinitionHandle Generate(FieldDefinition field)
        {
            var fieldSignature = fieldSignatureGenerator.GenerateSignatureOf(field);
            var fieldDefinitionHandle = metadataContainer.MetadataBuilder.AddFieldDefinition(
                attributes: GetFieldAttributesFor(field),
                name: metadataContainer.MetadataBuilder.GetOrAddString(field.Name),
                signature: metadataContainer.MetadataBuilder.GetOrAddBlob(fieldSignature));

            if (field.SpecifiesRelativeVirtualAddress)
            {
                var offset = metadataContainer.MappedFieldData.Count;
                metadataContainer.MappedFieldData.WriteBytes((byte[]) field.Value.Value);
                metadataContainer.MappedFieldData.Align(ManagedPEBuilder.MappedFieldDataAlignment);
                metadataContainer.MetadataBuilder.AddFieldRelativeVirtualAddress(fieldDefinitionHandle, offset);
            }
            else if (field.Value != null)
            {
                metadataContainer.MetadataBuilder.AddConstant(fieldDefinitionHandle, field.Value.Value);
            }

            foreach (var customAttribute in field.Attributes)
            {
                customAttributeGenerator.Generate(fieldDefinitionHandle, customAttribute);
            }

            return fieldDefinitionHandle;
        }
    }
}
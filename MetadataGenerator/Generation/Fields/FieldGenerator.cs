using System.Reflection.PortableExecutable;
using MetadataGenerator.Generation.CustomAttributes;
using Model.Types;
using static MetadataGenerator.Generation.AttributesProvider;
using SRM = System.Reflection.Metadata;

namespace MetadataGenerator.Generation.Fields
{
    internal class FieldGenerator
    {
        private readonly MetadataContainer metadataContainer;
        private readonly CustomAttributeGenerator customAttributeGenerator;

        public FieldGenerator(MetadataContainer metadataContainer)
        {
            this.metadataContainer = metadataContainer;
            customAttributeGenerator = new CustomAttributeGenerator(metadataContainer);
        }

        public SRM.FieldDefinitionHandle Generate(FieldDefinition field)
        {
            var metadataBuilder = metadataContainer.MetadataBuilder;
            var fieldSignature = metadataContainer.FieldSignatureEncoder.EncodeSignatureOf(field);

            // Field Table (0x04)
            var fieldDefinitionHandle = metadataBuilder.AddFieldDefinition(
                attributes: AttributesFor(field),
                name: metadataBuilder.GetOrAddString(field.Name),
                signature: metadataBuilder.GetOrAddBlob(fieldSignature));

            // add initial value
            if (field.SpecifiesRelativeVirtualAddress)
            {
                // Static fields can define their initial value as a constant stored in the PE File. The value is declared using the .data directive,
                // represented as a bytearray and labeled so it can be referenced later.
                //
                // Initialization of a static array field is an example of this. The field itself does not hold the initial value.
                // A special type is created (<PrivateImplementationDetails>) that has a field that references the value declared with .data.
                // It is then used in the constructor of the class that has the static array field to initialize it.
                var offset = metadataContainer.MappedFieldData.Count;
                metadataContainer.MappedFieldData.WriteBytes((byte[]) field.Value.Value);
                metadataContainer.MappedFieldData.Align(ManagedPEBuilder.MappedFieldDataAlignment);
                metadataBuilder.AddFieldRelativeVirtualAddress(fieldDefinitionHandle, offset);
            }
            else if (field.Value != null)
            {
                metadataBuilder.AddConstant(fieldDefinitionHandle, field.Value.Value);
            }

            foreach (var customAttribute in field.Attributes)
            {
                customAttributeGenerator.Generate(fieldDefinitionHandle, customAttribute);
            }

            return fieldDefinitionHandle;
        }
    }
}
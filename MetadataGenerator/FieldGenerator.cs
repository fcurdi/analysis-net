using Model.Types;
using static MetadataGenerator.AttributesProvider;
using ECMA335 = System.Reflection.Metadata.Ecma335;
using SRM = System.Reflection.Metadata;

namespace MetadataGenerator
{
    public class FieldGenerator
    {
        private readonly ECMA335.MetadataBuilder metadata;
        private readonly TypeEncoder typeEncoder;

        public FieldGenerator(ECMA335.MetadataBuilder metadata, TypeEncoder typeEncoder)
        {
            this.metadata = metadata;
            this.typeEncoder = typeEncoder;
        }

        public SRM.FieldDefinitionHandle Generate(FieldDefinition field)
        {
            var fieldSignature = new SRM.BlobBuilder();
            typeEncoder.Encode(field.Type, new ECMA335.BlobEncoder(fieldSignature).FieldSignature());
            var fieldDefinitionHandle = metadata.AddFieldDefinition(
                    attributes: GetFieldAttributesFor(field),
                    name: metadata.GetOrAddString(field.Name),
                    signature: metadata.GetOrAddBlob(fieldSignature));

            if (field.Value != null)
            {
                metadata.AddConstant(fieldDefinitionHandle, field.Value.Value);
            }

            return fieldDefinitionHandle;
        }
    }
}
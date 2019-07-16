using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace MetadataGenerator
{
    public class FieldGenerator
    {
        private readonly MetadataBuilder metadata;
        private int nextOffset;
        private readonly TypeEncoder typeEncoder;

        public FieldGenerator(MetadataBuilder metadata, TypeEncoder typeEncoder)
        {
            this.metadata = metadata;
            this.nextOffset = 1;
            this.typeEncoder = typeEncoder;
        }

        public FieldDefinitionHandle Generate(Model.Types.FieldDefinition field)
        {
            var fieldSignatureBlobBuilder = new BlobBuilder();
            var fieldSignature = new BlobEncoder(fieldSignatureBlobBuilder).FieldSignature();

            //FIXME: should be: if it is a custom type (not primitive nor primitive wrapper)
            //FIXME: in the examples for the only one that needs this is the enum values
            //FIXME: and the field value__ that is generated for the enum it is always a primitive so it needs to go to the else branch
            if (field.ContainingType.Kind.Equals(Model.Types.TypeDefinitionKind.Enum) && !field.Name.Equals("value__"))
            {
                fieldSignature.Type(
                    metadata.AddTypeReference( //FIXME: this should be done only once per type. 
                        default(AssemblyReferenceHandle),
                        metadata.GetOrAddString(field.ContainingType.ContainingNamespace.Name),
                        metadata.GetOrAddString(field.ContainingType.Name)),
                    true);
            }
            else
            {
                typeEncoder.Encode(field.Type, fieldSignature);
            }

            var fieldDefinitionHandle = metadata.AddFieldDefinition(
                    attributes: AttributesProvider.GetAttributesFor(field),
                    name: metadata.GetOrAddString(field.Name),
                    signature: metadata.GetOrAddBlob(fieldSignatureBlobBuilder));


            if (field.Value != null)
            {
                metadata.AddConstant(fieldDefinitionHandle, field.Value.Value);
            }

            nextOffset++;

            return fieldDefinitionHandle;

        }

        public FieldDefinitionHandle NextFieldHandle()
        {
            return MetadataTokens.FieldDefinitionHandle(nextOffset);
        }
    }
}

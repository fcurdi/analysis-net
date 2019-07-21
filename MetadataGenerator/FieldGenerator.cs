﻿using System.Reflection.Metadata;
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
            typeEncoder.Encode(field.Type, fieldSignature);
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

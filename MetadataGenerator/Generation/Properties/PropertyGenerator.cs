using System.Collections.Generic;
using MetadataGenerator.Generation.CustomAttributes;
using Model.Types;
using ECMA335 = System.Reflection.Metadata.Ecma335;
using SR = System.Reflection;
using SRM = System.Reflection.Metadata;

namespace MetadataGenerator.Generation.Properties
{
    internal class PropertyGenerator
    {
        private readonly MetadataContainer metadataContainer;
        private readonly CustomAttributeGenerator customAttributeGenerator;

        public PropertyGenerator(MetadataContainer metadataContainer, CustomAttributeGenerator customAttributeGenerator)
        {
            this.metadataContainer = metadataContainer;
            this.customAttributeGenerator = customAttributeGenerator;
        }

        // Properties can have getters or setters which are methods defined within the class.
        // methodToHandle is the association of methods to their handles. 
        public SRM.PropertyDefinitionHandle Generate(
            PropertyDefinition property,
            IDictionary<MethodDefinition, SRM.MethodDefinitionHandle> methodToHandle)
        {
            var signature = metadataContainer
                .Encoders
                .PropertySignatureEncoder
                .EncodeSignatureOf(property);

            // Property Table (0x17)
            var propertyDefinitionHandle = metadataContainer.MetadataBuilder.AddProperty(
                attributes: SR.PropertyAttributes.None,
                name: metadataContainer.MetadataBuilder.GetOrAddString(property.Name),
                signature: metadataContainer.MetadataBuilder.GetOrAddBlob(signature));

            if (property.Getter != null)
            {
                // MethodSemantics Table (0x18)
                metadataContainer.MetadataBuilder.AddMethodSemantics(
                    propertyDefinitionHandle,
                    SR.MethodSemanticsAttributes.Getter,
                    methodToHandle[property.Getter]);
            }

            if (property.Setter != null)
            {
                // MethodSemantics Table (0x18)
                metadataContainer.MetadataBuilder.AddMethodSemantics(
                    propertyDefinitionHandle,
                    SR.MethodSemanticsAttributes.Setter,
                    methodToHandle[property.Setter]);
            }

            foreach (var customAttribute in property.Attributes)
            {
                customAttributeGenerator.Generate(propertyDefinitionHandle, customAttribute);
            }

            return propertyDefinitionHandle;
        }
    }
}
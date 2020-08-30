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
        private readonly PropertySignatureEncoder propertySignatureEncoder;
        private readonly CustomAttributeGenerator customAttributeGenerator;

        public PropertyGenerator(MetadataContainer metadataContainer)
        {
            this.metadataContainer = metadataContainer;
            propertySignatureEncoder = new PropertySignatureEncoder(metadataContainer.MetadataResolver);
            customAttributeGenerator = new CustomAttributeGenerator(metadataContainer);
        }

        // Properties can have getters or setters which are methods defined within the class.
        // methodDefHandleOf is the association of methods to their handles. 
        public SRM.PropertyDefinitionHandle Generate(
            PropertyDefinition property,
            IDictionary<MethodDefinition, SRM.MethodDefinitionHandle> methodDefHandleOf)
        {
            var signature = propertySignatureEncoder.EncodeSignatureOf(property);
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
                    methodDefHandleOf[property.Getter]);
            }

            if (property.Setter != null)
            {
                // MethodSemantics Table (0x18)
                metadataContainer.MetadataBuilder.AddMethodSemantics(
                    propertyDefinitionHandle,
                    SR.MethodSemanticsAttributes.Setter,
                    methodDefHandleOf[property.Setter]);
            }

            foreach (var customAttribute in property.Attributes)
            {
                customAttributeGenerator.Generate(propertyDefinitionHandle, customAttribute);
            }

            return propertyDefinitionHandle;
        }
    }
}
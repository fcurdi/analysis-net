using System.Collections.Generic;
using MetadataGenerator.Metadata;
using Model.Types;
using ECMA335 = System.Reflection.Metadata.Ecma335;
using SR = System.Reflection;
using SRM = System.Reflection.Metadata;

namespace MetadataGenerator.Generators
{
    internal class PropertyGenerator
    {
        private readonly MetadataContainer metadataContainer;
        private readonly CustomAttributeGenerator customAttributeGenerator;

        public PropertyGenerator(MetadataContainer metadataContainer)
        {
            this.metadataContainer = metadataContainer;
            customAttributeGenerator = new CustomAttributeGenerator(metadataContainer);
        }

        public SRM.PropertyDefinitionHandle Generate(
            PropertyDefinition property,
            IDictionary<MethodDefinition, SRM.MethodDefinitionHandle> methodDefHandleOf)
        {
            var signature = new SRM.BlobBuilder();
            new ECMA335.BlobEncoder(signature)
                .PropertySignature(isInstanceProperty: !property.IsStatic)
                .Parameters(
                    parameterCount: 0,
                    returnType: returnTypeEncoder => metadataContainer.MetadataResolver.Encode(property.PropertyType, returnTypeEncoder.Type()),
                    parameters: parametersEncoder => { });

            var propertyDefinitionHandle = metadataContainer.MetadataBuilder.AddProperty(
                attributes: SR.PropertyAttributes.None,
                name: metadataContainer.MetadataBuilder.GetOrAddString(property.Name),
                signature: metadataContainer.MetadataBuilder.GetOrAddBlob(signature));

            if (property.Getter != null)
            {
                metadataContainer.MetadataBuilder.AddMethodSemantics(
                    propertyDefinitionHandle,
                    SR.MethodSemanticsAttributes.Getter,
                    methodDefHandleOf[property.Getter]);
            }

            if (property.Setter != null)
            {
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
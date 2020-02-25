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

        public PropertyGenerator(MetadataContainer metadataContainer)
        {
            this.metadataContainer = metadataContainer;
        }

        public SRM.PropertyDefinitionHandle Generate(
            PropertyDefinition property,
            IDictionary<MethodDefinition, SRM.MethodDefinitionHandle> methodDefHandleOf)
        {
            var signature = new SRM.BlobBuilder();
            new ECMA335.BlobEncoder(signature)
                .PropertySignature(isInstanceProperty: property.IsInstanceProperty)
                .Parameters(
                    parameterCount: 0,
                    returnType: returnTypeEncoder => metadataContainer.metadataResolver.Encode(property.PropertyType, returnTypeEncoder.Type()),
                    parameters: parametersEncoder => { });

            var propertyDefinitionHandle = metadataContainer.metadataBuilder.AddProperty(
                attributes: SR.PropertyAttributes.None,
                name: metadataContainer.metadataBuilder.GetOrAddString(property.Name),
                signature: metadataContainer.metadataBuilder.GetOrAddBlob(signature));

            if (property.Getter != null)
            {
                metadataContainer.metadataBuilder.AddMethodSemantics(
                    propertyDefinitionHandle,
                    SR.MethodSemanticsAttributes.Getter,
                    methodDefHandleOf[property.Getter]);
            }

            if (property.Setter != null)
            {
                metadataContainer.metadataBuilder.AddMethodSemantics(
                    propertyDefinitionHandle,
                    SR.MethodSemanticsAttributes.Setter,
                    methodDefHandleOf[property.Setter]);
            }

            return propertyDefinitionHandle;
        }
    }
}
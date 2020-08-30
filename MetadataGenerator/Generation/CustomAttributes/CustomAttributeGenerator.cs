using Model.Types;
using ECMA335 = System.Reflection.Metadata.Ecma335;
using SRM = System.Reflection.Metadata;

namespace MetadataGenerator.Generation.CustomAttributes
{
    internal class CustomAttributeGenerator
    {
        private readonly MetadataContainer metadataContainer;

        public CustomAttributeGenerator(MetadataContainer metadataContainer)
        {
            this.metadataContainer = metadataContainer;
        }

        public void Generate(SRM.EntityHandle owner, CustomAttribute customAttribute)
        {
            var signature = CustomAttributesSignatureEncoder.EncodeSignatureOf(customAttribute);
            // CustomAttribute Table (0x0C)
            metadataContainer.MetadataBuilder.AddCustomAttribute(
                owner,
                metadataContainer.MetadataResolver.HandleOf(customAttribute.Constructor),
                metadataContainer.MetadataBuilder.GetOrAddBlob(signature));
        }
    }
}
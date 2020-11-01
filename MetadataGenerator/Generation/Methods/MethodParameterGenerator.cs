using Model.Types;
using SRM = System.Reflection.Metadata;
using static MetadataGenerator.Generation.AttributesProvider;

namespace MetadataGenerator.Generation.Methods
{
    internal class MethodParameterGenerator
    {
        private readonly MetadataContainer metadataContainer;

        internal MethodParameterGenerator(MetadataContainer metadataContainer)
        {
            this.metadataContainer = metadataContainer;
        }

        public SRM.ParameterHandle Generate(MethodParameter methodParameter)
        {
            // Param Table (0x08)
            var parameterHandle = metadataContainer.MetadataBuilder.AddParameter(
                attributes: AttributesFor(methodParameter),
                name: metadataContainer.MetadataBuilder.GetOrAddString(methodParameter.Name),
                sequenceNumber: methodParameter.Index);

            if (methodParameter.HasDefaultValue)
            {
                // Constant Table (0x0B) 
                metadataContainer.MetadataBuilder.AddConstant(parameterHandle, methodParameter.DefaultValue.Value);
            }

            return parameterHandle;
        }
    }
}
using System.Collections.Generic;
using Model.Types;
using SRM = System.Reflection.Metadata;
using static MetadataGenerator.AttributesProvider;

namespace MetadataGenerator.Generation.Methods
{
    internal class MethodParametersGenerator
    {
        private readonly MetadataContainer metadataContainer;

        internal MethodParametersGenerator(MetadataContainer metadataContainer)
        {
            this.metadataContainer = metadataContainer;
        }

        public SRM.ParameterHandle? Generate(IList<MethodParameter> methodParameters)
        {
            SRM.ParameterHandle? firstParameterHandle = null;
            foreach (var parameter in methodParameters)
            {
                var parameterHandle = metadataContainer.MetadataBuilder.AddParameter(
                    attributes: AttributesFor(parameter),
                    name: metadataContainer.MetadataBuilder.GetOrAddString(parameter.Name),
                    sequenceNumber: parameter.Index);
                if (parameter.HasDefaultValue)
                {
                    metadataContainer.MetadataBuilder.AddConstant(parameterHandle, parameter.DefaultValue.Value);
                }

                if (!firstParameterHandle.HasValue)
                {
                    firstParameterHandle = parameterHandle;
                }
            }

            return firstParameterHandle;
        }
    }
}
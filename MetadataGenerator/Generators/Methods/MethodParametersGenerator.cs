using System.Collections.Generic;
using MetadataGenerator.Metadata;
using Model.Types;
using ECMA335 = System.Reflection.Metadata.Ecma335;
using SR = System.Reflection;
using SRM = System.Reflection.Metadata;
using static MetadataGenerator.Metadata.AttributesProvider;

namespace MetadataGenerator.Generators.Methods
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
                var parameterHandle = metadataContainer.metadataBuilder.AddParameter(
                    attributes: GetParameterAttributesFor(parameter),
                    name: metadataContainer.metadataBuilder.GetOrAddString(parameter.Name),
                    sequenceNumber: parameter.Index);
                if (parameter.HasDefaultValue)
                {
                    metadataContainer.metadataBuilder.AddConstant(parameterHandle, parameter.DefaultValue.Value);
                }

                if (!firstParameterHandle.HasValue)
                {
                    firstParameterHandle = parameterHandle;
                }

                /* TODO add custom attributes (ex: varargs), see ECMA under custom attributes
                metadataContainer.metadataBuilder.AddCustomAttribute(
                    parameter handle,
                    some handle,
                    value
                )
                */
            }

            return firstParameterHandle;
        }
    }
}
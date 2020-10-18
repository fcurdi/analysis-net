using System.Linq;
using MetadataGenerator.Generation.CustomAttributes;
using MetadataGenerator.Generation.Methods.Body;
using Model.Types;
using static MetadataGenerator.Generation.AttributesProvider;
using ECMA335 = System.Reflection.Metadata.Ecma335;
using SR = System.Reflection;
using SRM = System.Reflection.Metadata;

namespace MetadataGenerator.Generation.Methods
{
    internal class MethodGenerator
    {
        private readonly MetadataContainer metadataContainer;
        private readonly MethodParameterGenerator methodParameterGenerator;
        private readonly CustomAttributeGenerator customAttributeGenerator;

        public MethodGenerator(MetadataContainer metadataContainer)
        {
            this.metadataContainer = metadataContainer;
            methodParameterGenerator = new MethodParameterGenerator(metadataContainer);
            customAttributeGenerator = new CustomAttributeGenerator(metadataContainer);
        }

        public SRM.MethodDefinitionHandle Generate(MethodDefinition method)
        {
            var methodParameterHandles = method
                .Parameters
                .Select(parameter => methodParameterGenerator.Generate(parameter))
                .ToList();
            var methodSignature = metadataContainer.MethodSignatureEncoder.EncodeSignatureOf(method);
            var methodBodyOffset = -1;
            if (method.HasBody)
            {
                var localVariablesSignatureHandle = method.Body.LocalVariables.Count > 0
                    ? metadataContainer.HandleResolver.HandleOf(method.Body.LocalVariables)
                    : default;

                var instructionEncoder = new MethodBodyEncoder(metadataContainer.HandleResolver, method.Body).Encode(out var maxStack);
                methodBodyOffset = metadataContainer.MethodBodyStream.AddMethodBody(
                    instructionEncoder: instructionEncoder,
                    localVariablesSignature: localVariablesSignatureHandle,
                    maxStack: maxStack);
            }

            var methodImplementationAttributes =
                SR.MethodImplAttributes.IL |
                (!method.HasBody && !method.IsAbstract ? SR.MethodImplAttributes.Runtime : SR.MethodImplAttributes.Managed);

            var nextParameterHandle =
                ECMA335.MetadataTokens.ParameterHandle(metadataContainer.MetadataBuilder.NextRowFor(ECMA335.TableIndex.Param));
            // MethodDef Table (0x06) 
            var methodDefinitionHandle = metadataContainer.MetadataBuilder.AddMethodDefinition(
                attributes: AttributesFor(method),
                implAttributes: methodImplementationAttributes,
                name: metadataContainer.MetadataBuilder.GetOrAddString(method.Name),
                signature: metadataContainer.MetadataBuilder.GetOrAddBlob(methodSignature),
                bodyOffset: methodBodyOffset,
                parameterList: methodParameterHandles.FirstOr(nextParameterHandle));

            foreach (var customAttribute in method.Attributes)
            {
                customAttributeGenerator.Generate(methodDefinitionHandle, customAttribute);
            }

            if (method.Name.Equals("Main"))
            {
                metadataContainer.HandleResolver.MainMethodHandle = methodDefinitionHandle;
            }

            return methodDefinitionHandle;
        }
    }
}
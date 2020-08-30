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
        private readonly MethodSignatureEncoder methodSignatureEncoder;
        private readonly MethodLocalsSignatureEncoder methodLocalsSignatureEncoder;
        private readonly MethodParameterGenerator methodParameterGenerator;
        private readonly CustomAttributeGenerator customAttributeGenerator;

        public MethodGenerator(MetadataContainer metadataContainer)
        {
            this.metadataContainer = metadataContainer;
            methodSignatureEncoder = new MethodSignatureEncoder(metadataContainer.MetadataResolver);
            methodLocalsSignatureEncoder = new MethodLocalsSignatureEncoder(metadataContainer.MetadataResolver);
            methodParameterGenerator = new MethodParameterGenerator(metadataContainer);
            customAttributeGenerator = new CustomAttributeGenerator(metadataContainer);
        }

        public SRM.MethodDefinitionHandle Generate(MethodDefinition method)
        {
            var methodParameterHandles = method
                .Parameters
                .Select(parameter => methodParameterGenerator.Generate(parameter))
                .ToList();
            var methodSignature = methodSignatureEncoder.EncodeSignatureOf(method);
            var methodBodyOffset = -1;
            if (method.HasBody)
            {
                SRM.StandaloneSignatureHandle localVariablesSignature = default;
                if (method.Body.LocalVariables.Count > 0)
                {
                    var signature = methodLocalsSignatureEncoder.EncodeSignatureOf(method.Body.LocalVariables);
                    localVariablesSignature = metadataContainer.MetadataResolver.GetOrAddStandaloneSignature(signature);
                }

                var instructionEncoder = new MethodBodyEncoder(metadataContainer.MetadataResolver, method.Body).Encode(out var maxStack);
                methodBodyOffset = metadataContainer.MethodBodyStream.AddMethodBody(
                    instructionEncoder: instructionEncoder,
                    localVariablesSignature: localVariablesSignature,
                    maxStack: maxStack);
            }

            var methodImplementationAttributes =
                SR.MethodImplAttributes.IL |
                (!method.HasBody && !method.IsAbstract ? SR.MethodImplAttributes.Runtime : SR.MethodImplAttributes.Managed);

            var nextParameterHandle = ECMA335.MetadataTokens.ParameterHandle(metadataContainer.MetadataBuilder.NextRowFor(ECMA335.TableIndex.Param));
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
                metadataContainer.MainMethodHandle = methodDefinitionHandle;
            }

            return methodDefinitionHandle;
        }
    }
}
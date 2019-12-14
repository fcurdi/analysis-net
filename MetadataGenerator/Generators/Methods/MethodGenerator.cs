using MetadataGenerator.Generators.Methods.Body;
using MetadataGenerator.Metadata;
using Model.Types;
using static MetadataGenerator.Metadata.AttributesProvider;
using ECMA335 = System.Reflection.Metadata.Ecma335;
using SR = System.Reflection;
using SRM = System.Reflection.Metadata;

namespace MetadataGenerator.Generators.Methods
{
    internal class MethodGenerator
    {
        private readonly MetadataContainer metadataContainer;
        private readonly MethodSignatureGenerator methodSignatureGenerator;
        private readonly MethodBodyGenerator methodBodyGenerator;
        private readonly MethodLocalsGenerator methodLocalsGenerator;

        public MethodGenerator(MetadataContainer metadataContainer)
        {
            this.metadataContainer = metadataContainer;
            methodSignatureGenerator = new MethodSignatureGenerator(metadataContainer);
            methodBodyGenerator = new MethodBodyGenerator(metadataContainer);
            methodLocalsGenerator = new MethodLocalsGenerator(metadataContainer);
        }

        public SRM.MethodDefinitionHandle Generate(MethodDefinition method)
        {
            var methodSignature = methodSignatureGenerator.GenerateSignatureOf(method);
            SRM.ParameterHandle? firstParameterHandle = null;
            foreach (var parameter in method.Parameters)
            {
                var parameterHandle = metadataContainer.metadataBuilder.AddParameter(
                    GetParameterAttributesFor(parameter),
                    metadataContainer.metadataBuilder.GetOrAddString(parameter.Name),
                    parameter.Index);
                if (!firstParameterHandle.HasValue)
                {
                    firstParameterHandle = parameterHandle;
                }
            }

            // FIXME maxStack should be computed from instructions. When a dll is read, the maxStack will be available (Model) but if code is generated 
            // programatically then the maxStack is gonna be missing
            var methodBody = method.HasBody
                ? metadataContainer.methodBodyStream.AddMethodBody(
                    instructionEncoder: methodBodyGenerator.Generate(method.Body),
                    localVariablesSignature: methodLocalsGenerator.GenerateLocalVariablesSignatureFor(method.Body),
                    maxStack: method.Body.MaxStack)
                : default;

            var nextParameterHandle = ECMA335.MetadataTokens.ParameterHandle(metadataContainer.metadataBuilder.NextRowFor(ECMA335.TableIndex.Param));
            var methodDefinitionHandle = metadataContainer.metadataBuilder.AddMethodDefinition(
                attributes: GetMethodAttributesFor(method),
                implAttributes: SR.MethodImplAttributes.IL | SR.MethodImplAttributes.Managed, // FIXME what else?
                name: metadataContainer.metadataBuilder.GetOrAddString(method.Name),
                signature: metadataContainer.metadataBuilder.GetOrAddBlob(methodSignature),
                bodyOffset: methodBody,
                parameterList: firstParameterHandle ?? nextParameterHandle);

            methodLocalsGenerator.GenerateLocalVariables(method.Body, methodDefinitionHandle);

            return methodDefinitionHandle;
        }
    }
}
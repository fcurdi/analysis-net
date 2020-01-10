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
        private readonly MethodParametersGenerator methodParametersGenerator;

        public MethodGenerator(MetadataContainer metadataContainer)
        {
            this.metadataContainer = metadataContainer;
            methodSignatureGenerator = new MethodSignatureGenerator(metadataContainer);
            methodBodyGenerator = new MethodBodyGenerator(metadataContainer);
            methodLocalsGenerator = new MethodLocalsGenerator(metadataContainer);
            methodParametersGenerator = new MethodParametersGenerator(metadataContainer);
        }

        public SRM.MethodDefinitionHandle Generate(MethodDefinition method)
        {
            var parameters = methodParametersGenerator.Generate(method.Parameters)
                             ?? ECMA335.MetadataTokens.ParameterHandle(metadataContainer.metadataBuilder.NextRowFor(ECMA335.TableIndex.Param));
            var methodSignature = methodSignatureGenerator.GenerateSignatureOf(method);

            var methodBodyOffset = -1;
            var bodyLengthInBytes = 0;
            if (method.HasBody)
            {
                // FIXME maxStack should be computed from instructions. When a dll is read, the maxStack will be available (Model) but if code is generated 
                // programatically then the maxStack is gonna be missing
                var maxStack = method.Body.MaxStack;
                var instructionEncoder = methodBodyGenerator.Generate(method.Body);
                methodBodyOffset = metadataContainer.methodBodyStream.AddMethodBody(
                    instructionEncoder: instructionEncoder,
                    localVariablesSignature: methodLocalsGenerator.GenerateLocalVariablesSignatureFor(method.Body),
                    maxStack: maxStack);
                bodyLengthInBytes = instructionEncoder.Offset;
            }

            var methodDefinitionHandle = metadataContainer.metadataBuilder.AddMethodDefinition(
                attributes: GetMethodAttributesFor(method),
                implAttributes: SR.MethodImplAttributes.IL | SR.MethodImplAttributes.Managed,
                name: metadataContainer.metadataBuilder.GetOrAddString(method.Name),
                signature: metadataContainer.metadataBuilder.GetOrAddBlob(methodSignature),
                bodyOffset: methodBodyOffset,
                parameterList: parameters);

            methodLocalsGenerator.GenerateLocalVariables(method.Body, methodDefinitionHandle, bodyLengthInBytes);

            return methodDefinitionHandle;
        }
    }
}
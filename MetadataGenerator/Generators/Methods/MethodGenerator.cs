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
        private readonly MethodLocalsSignatureGenerator methodLocalsSignatureGenerator;
        private readonly MethodParametersGenerator methodParametersGenerator;

        public MethodGenerator(MetadataContainer metadataContainer)
        {
            this.metadataContainer = metadataContainer;
            methodSignatureGenerator = new MethodSignatureGenerator(metadataContainer);
            methodBodyGenerator = new MethodBodyGenerator(metadataContainer);
            methodLocalsSignatureGenerator = new MethodLocalsSignatureGenerator(metadataContainer);
            methodParametersGenerator = new MethodParametersGenerator(metadataContainer);
        }

        public SRM.MethodDefinitionHandle Generate(MethodDefinition method)
        {
            var parameters = methodParametersGenerator.Generate(method.Parameters)
                             ?? ECMA335.MetadataTokens.ParameterHandle(metadataContainer.metadataBuilder.NextRowFor(ECMA335.TableIndex.Param));
            var methodSignature = methodSignatureGenerator.GenerateSignatureOf(method);

            var methodBodyOffset = -1;
            if (method.HasBody)
            {
                // FIXME maxStack should be computed from instructions. When a dll is read, the maxStack will be available (Model) but if code is generated 
                // programatically then the maxStack is gonna be missing
                var maxStack = method.Body.MaxStack;
                methodBodyOffset = metadataContainer.methodBodyStream.AddMethodBody(
                    instructionEncoder: methodBodyGenerator.Generate(method.Body),
                    localVariablesSignature: methodLocalsSignatureGenerator.GenerateSignatureFor(method.Body.LocalVariables),
                    maxStack: maxStack);
            }

            var methodImplementationAttributes =
                SR.MethodImplAttributes.IL |
                (!method.HasBody && !method.IsAbstract // FIXME Could also be PinvokeImpl or InternalCall in some special cases 
                    ? SR.MethodImplAttributes.Runtime
                    : SR.MethodImplAttributes.Managed);

            var methodDefinitionHandle = metadataContainer.metadataBuilder.AddMethodDefinition(
                attributes: GetMethodAttributesFor(method),
                implAttributes: methodImplementationAttributes,
                name: metadataContainer.metadataBuilder.GetOrAddString(method.Name),
                signature: metadataContainer.metadataBuilder.GetOrAddBlob(methodSignature),
                bodyOffset: methodBodyOffset,
                parameterList: parameters);

            return methodDefinitionHandle;
        }
    }
}
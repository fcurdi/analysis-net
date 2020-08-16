using System.Linq;
using Backend.Analyses;
using Backend.Transformations.Assembly;
using MetadataGenerator.Generators.Methods.Body;
using MetadataGenerator.Metadata;
using Model.Bytecode;
using Model.ThreeAddressCode.Instructions;
using Model.Types;
using static MetadataGenerator.Metadata.AttributesProvider;
using BranchInstruction = Model.Bytecode.BranchInstruction;
using BranchOperation = Model.Bytecode.BranchOperation;
using ECMA335 = System.Reflection.Metadata.Ecma335;
using SR = System.Reflection;
using SRM = System.Reflection.Metadata;

namespace MetadataGenerator.Generators.Methods
{
    internal class MethodGenerator
    {
        private readonly MetadataContainer metadataContainer;
        private readonly MethodSignatureGenerator methodSignatureGenerator;
        private readonly MethodLocalsSignatureGenerator methodLocalsSignatureGenerator;
        private readonly MethodParametersGenerator methodParametersGenerator;
        private readonly CustomAttributeGenerator customAttributeGenerator;

        public MethodGenerator(MetadataContainer metadataContainer)
        {
            this.metadataContainer = metadataContainer;
            methodSignatureGenerator = new MethodSignatureGenerator(metadataContainer);
            methodLocalsSignatureGenerator = new MethodLocalsSignatureGenerator(metadataContainer);
            methodParametersGenerator = new MethodParametersGenerator(metadataContainer);
            customAttributeGenerator = new CustomAttributeGenerator(metadataContainer);
        }

        public SRM.MethodDefinitionHandle Generate(MethodDefinition method)
        {
            var parameters = methodParametersGenerator.Generate(method.Parameters)
                             ?? ECMA335.MetadataTokens.ParameterHandle(metadataContainer.MetadataBuilder.NextRowFor(ECMA335.TableIndex.Param));
            var methodSignature = methodSignatureGenerator.GenerateSignatureOf(method);

            var methodBodyOffset = -1;
            if (method.HasBody)
            {
                // FIXME PROBAR generar y correr tests (+pedump) CONVIRTIENDO Y SIN CONVERTIR 

                // FIXME undo this. Just for testing assembler.
                var tac = new Backend.Transformations.Disassembler(method).Execute();
                method.Body = tac;

                var cfanalysis = new ControlFlowAnalysis(method.Body);
                var cfg = cfanalysis.GenerateExceptionalControlFlow();

                var webAnalysis = new WebAnalysis(cfg);
                webAnalysis.Analyze();
                webAnalysis.Transform();
                method.Body.UpdateVariables();

                var typeInferenceAnalysis = new TypeInferenceAnalysis(cfg, method.ReturnType);
                typeInferenceAnalysis.Analyze();

                var bytecode = new Backend.Transformations.Assembly.Assembler(method).Execute();
                method.Body = bytecode;

                // FIXME maxStack should be computed from instructions. When a dll is read, the maxStack will be available (Model) but if code is generated 
                // programatically then the maxStamck is gonna be missing
                var maxStack = method.Body.MaxStack;
                methodBodyOffset = metadataContainer.MethodBodyStream.AddMethodBody(
                    instructionEncoder: new MethodBodyGenerator(metadataContainer, method.Body).Generate(),
                    localVariablesSignature: methodLocalsSignatureGenerator.GenerateSignatureFor(method.Body.LocalVariables),
                    maxStack: maxStack);
            }

            var methodImplementationAttributes =
                SR.MethodImplAttributes.IL |
                (!method.HasBody && !method.IsAbstract // FIXME Could also be PinvokeImpl or InternalCall in some special cases 
                    ? SR.MethodImplAttributes.Runtime
                    : SR.MethodImplAttributes.Managed);

            var methodDefinitionHandle = metadataContainer.MetadataBuilder.AddMethodDefinition(
                attributes: GetMethodAttributesFor(method),
                implAttributes: methodImplementationAttributes,
                name: metadataContainer.MetadataBuilder.GetOrAddString(method.Name),
                signature: metadataContainer.MetadataBuilder.GetOrAddBlob(methodSignature),
                bodyOffset: methodBodyOffset,
                parameterList: parameters);

            foreach (var customAttribute in method.Attributes)
            {
                customAttributeGenerator.Generate(methodDefinitionHandle, customAttribute);
            }

            return methodDefinitionHandle;
        }
    }
}
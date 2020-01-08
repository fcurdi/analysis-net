using MetadataGenerator.Metadata;
using Model.Types;
using ECMA335 = System.Reflection.Metadata.Ecma335;
using SRM = System.Reflection.Metadata;

namespace MetadataGenerator.Generators.Methods.Body
{
    internal class MethodLocalsGenerator
    {
        private readonly MetadataContainer metadataContainer;

        public MethodLocalsGenerator(MetadataContainer metadataContainer)
        {
            this.metadataContainer = metadataContainer;
        }

        public SRM.StandaloneSignatureHandle GenerateLocalVariablesSignatureFor(MethodBody body)
        {
            SRM.StandaloneSignatureHandle localVariablesSignature = default;
            if (body?.LocalVariables?.Count > 0)
            {
                var signature = new SRM.BlobBuilder();
                var encoder = new ECMA335.BlobEncoder(signature).LocalVariableSignature(body.LocalVariables.Count);
                foreach (var localVariable in body.LocalVariables)
                {
                    // TODO pinned is achieved by the fixed keyword and this is not in the model
                    metadataContainer.metadataResolver.Encode(localVariable.Type, encoder.AddVariable().Type(isPinned: false));
                }

                localVariablesSignature = metadataContainer.metadataResolver.GetOrAddStandaloneSignature(signature);
            }

            return localVariablesSignature;
        }

        public void GenerateLocalVariables(MethodBody body, SRM.MethodDefinitionHandle containingMethodHandle)
        {
            /* FIXME GenerateLocalVariablesSignatureFor solo genera la firma (que se usa para hacer el addMethodBody) 
             y no agrega las variables por lo que no deberia andar. Sin embargo parece que anda solo con eso. 
            Ver si es necesario este codigo de abajo o no
            No aparecen los nombres de las variables locales (similar a lo que pasaba con los parameters cuadno solo ponia la firma)
            Asi que tiene pinta a que es necesario hacer esto. Sin embargo al hacerlo no cambia nada.
            Quiza estan mal algunos de los valores de addLocalScope y por eso no anda
            */
            if (body?.LocalVariables?.Count > 0)
            {
                SRM.LocalVariableHandle? firstLocalVariableHandle = null;
                foreach (var localVariable in body.LocalVariables)
                {
                    var localVariableHandle = metadataContainer.metadataBuilder.AddLocalVariable(
                        attributes: SRM.LocalVariableAttributes.None,
                        index: body.LocalVariables.IndexOf(localVariable),
                        name: metadataContainer.metadataBuilder.GetOrAddString(localVariable.Name));
                    if (!firstLocalVariableHandle.HasValue)
                    {
                        firstLocalVariableHandle = localVariableHandle;
                    }
                }

                var nextLocalVariableHandle =
                    ECMA335.MetadataTokens.LocalVariableHandle(metadataContainer.metadataBuilder.NextRowFor(ECMA335.TableIndex.LocalVariable));

                var nextLocalConstantHandle =
                    ECMA335.MetadataTokens.LocalConstantHandle(metadataContainer.metadataBuilder.NextRowFor(ECMA335.TableIndex.LocalConstant));

                var nextImportScopeHandle =
                    ECMA335.MetadataTokens.ImportScopeHandle(metadataContainer.metadataBuilder.NextRowFor(ECMA335.TableIndex.ImportScope));

                metadataContainer.metadataBuilder.AddLocalScope(
                    method: containingMethodHandle,
                    importScope: nextImportScopeHandle, // addImportScope() ?
                    variableList: firstLocalVariableHandle ?? nextLocalVariableHandle,
                    constantList: nextLocalConstantHandle, // addLocalConstant() ?
                    startOffset: default, // FIXME ??
                    length: default); // FIXME ??
            }
        }
    }
}
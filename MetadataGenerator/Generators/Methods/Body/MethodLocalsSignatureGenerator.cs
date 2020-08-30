using System.Collections.Generic;
using Model.ThreeAddressCode.Values;
using ECMA335 = System.Reflection.Metadata.Ecma335;
using SRM = System.Reflection.Metadata;

namespace MetadataGenerator.Generators.Methods.Body
{
    internal class MethodLocalsSignatureGenerator
    {
        private readonly MetadataContainer metadataContainer;

        public MethodLocalsSignatureGenerator(MetadataContainer metadataContainer)
        {
            this.metadataContainer = metadataContainer;
        }

        public SRM.StandaloneSignatureHandle GenerateSignatureFor(IList<IVariable> localVariables)
        {
            SRM.StandaloneSignatureHandle localVariablesSignature = default;
            if (localVariables.Count > 0)
            {
                var signature = new SRM.BlobBuilder();
                var encoder = new ECMA335.BlobEncoder(signature).LocalVariableSignature(localVariables.Count);
                foreach (var localVariable in localVariables)
                {
                    metadataContainer.MetadataResolver.Encode(localVariable.Type, encoder.AddVariable().Type(isPinned: false));
                }

                localVariablesSignature = metadataContainer.MetadataResolver.GetOrAddStandaloneSignature(signature);
            }

            return localVariablesSignature;
        }
    }
}
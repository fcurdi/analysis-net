using System;
using System.Collections.Generic;
using Model.ThreeAddressCode.Values;
using ECMA335 = System.Reflection.Metadata.Ecma335;
using SRM = System.Reflection.Metadata;

namespace MetadataGenerator.Generation.Methods.Body
{
    internal class MethodLocalsSignatureEncoder
    {
        private readonly HandleResolver _handleResolver;

        public MethodLocalsSignatureEncoder(HandleResolver handleResolver)
        {
            this._handleResolver = handleResolver;
        }

        public SRM.BlobBuilder EncodeSignatureOf(IList<IVariable> localVariables)
        {
            if (localVariables.Count == 0) throw new Exception("No local variables to generate signature for");

            var signature = new SRM.BlobBuilder();
            var encoder = new ECMA335.BlobEncoder(signature).LocalVariableSignature(localVariables.Count);
            foreach (var localVariable in localVariables)
            {
                _handleResolver.Encode(localVariable.Type, encoder.AddVariable().Type(isPinned: false));
            }

            return signature;
        }
    }
}
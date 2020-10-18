using System;
using System.Collections.Generic;
using MetadataGenerator.Generation.Types;
using Model.ThreeAddressCode.Values;
using ECMA335 = System.Reflection.Metadata.Ecma335;
using SRM = System.Reflection.Metadata;

namespace MetadataGenerator.Generation.Methods.Body
{
    internal class MethodLocalsSignatureEncoder
    {
        private readonly TypeEncoder typeEncoder;

        public MethodLocalsSignatureEncoder(TypeEncoder typeEncoder)
        {
            this.typeEncoder = typeEncoder;
        }

        public SRM.BlobBuilder EncodeSignatureOf(IList<IVariable> localVariables)
        {
            if (localVariables.Count == 0) throw new Exception("No local variables to generate signature for");

            var signature = new SRM.BlobBuilder();
            var encoder = new ECMA335.BlobEncoder(signature).LocalVariableSignature(localVariables.Count);
            foreach (var localVariable in localVariables)
            {
                typeEncoder.Encode(localVariable.Type, encoder.AddVariable().Type(isPinned: false));
            }

            return signature;
        }
    }
}
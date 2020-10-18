using Model.Types;
using ECMA335 = System.Reflection.Metadata.Ecma335;
using SRM = System.Reflection.Metadata;

namespace MetadataGenerator.Generation.Types
{
    internal class TypeSignatureEncoder
    {
        private readonly TypeEncoder typeEncoder;

        public TypeSignatureEncoder(TypeEncoder typeEncoder)
        {
            this.typeEncoder = typeEncoder;
        }

        public SRM.BlobBuilder EncodeSignatureOf(IType type)
        {
            var signature = new SRM.BlobBuilder();
            typeEncoder.Encode(type, new ECMA335.BlobEncoder(signature).TypeSpecificationSignature());
            return signature;
        }
    }
}
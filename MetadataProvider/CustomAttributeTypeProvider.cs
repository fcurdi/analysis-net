using System.Reflection.Metadata;
using Model.Types;

namespace MetadataProvider
{
    internal class CustomAttributeTypeProvider : ICustomAttributeTypeProvider<IType>
    {
        private readonly SignatureTypeProvider signatureTypeProvider;

        public CustomAttributeTypeProvider(SignatureTypeProvider signatureTypeProvider)
        {
            this.signatureTypeProvider = signatureTypeProvider;
        }

        public IType GetPrimitiveType(PrimitiveTypeCode typeCode) => signatureTypeProvider.GetPrimitiveType(typeCode);

        public IType GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind) =>
            signatureTypeProvider.GetTypeFromDefinition(reader, handle, rawTypeKind);

        public IType GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind) =>
            signatureTypeProvider.GetTypeFromReference(reader, handle, rawTypeKind);

        public IType GetSZArrayType(IType elementType) => signatureTypeProvider.GetSZArrayType(elementType);

        public IType GetSystemType() => PlatformTypes.Type;

        public bool IsSystemType(IType type) => PlatformTypes.Type.Equals(type);

        public IType GetTypeFromSerializedName(string name) => new BasicType(name);

        public PrimitiveTypeCode GetUnderlyingEnumType(IType type) => PrimitiveTypeCode.Int32;
    }
}
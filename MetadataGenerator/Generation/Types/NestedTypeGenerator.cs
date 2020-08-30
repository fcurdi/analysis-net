using System.Linq;
using ECMA335 = System.Reflection.Metadata.Ecma335;
using SRM = System.Reflection.Metadata;

namespace MetadataGenerator.Generation.Types
{
    internal static class NestedTypeGenerator
    {
        // NestedClass Table (0x29) 
        public static void GenerateNestedTypes(MetadataContainer metadataContainer) =>
            metadataContainer
                .NestedTypeEntries
                .OrderBy(entry => ECMA335.CodedIndex.TypeDefOrRef(entry.Type))
                .ToList()
                .ForEach(entry => metadataContainer.MetadataBuilder.AddNestedType(entry.Type, entry.EnclosingType));

        public class NestedTypeEntry
        {
            public readonly SRM.TypeDefinitionHandle Type;
            public readonly SRM.TypeDefinitionHandle EnclosingType;

            public NestedTypeEntry(SRM.TypeDefinitionHandle type, SRM.TypeDefinitionHandle enclosingType)
            {
                Type = type;
                EnclosingType = enclosingType;
            }
        }
    }
}
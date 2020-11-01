using System.Linq;
using ECMA335 = System.Reflection.Metadata.Ecma335;
using SRM = System.Reflection.Metadata;

namespace MetadataGenerator.Generation.Types
{
    internal class NestedTypeGenerator
    {
        private readonly MetadataContainer metadataContainer;

        public NestedTypeGenerator(MetadataContainer metadataContainer)
        {
            this.metadataContainer = metadataContainer;
        }

        // NestedClass Table (0x29) 
        public void Generate() =>
            metadataContainer
                .DelayedEntries
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
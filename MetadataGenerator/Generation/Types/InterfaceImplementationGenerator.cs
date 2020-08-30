using System.Linq;
using ECMA335 = System.Reflection.Metadata.Ecma335;
using SRM = System.Reflection.Metadata;

namespace MetadataGenerator.Generation.Types
{
    internal static class InterfaceImplementationGenerator
    {
        // InterfaceImpl Table (0x09) 
        public static void GenerateInterfaceImplementations(MetadataContainer metadataContainer) =>
            metadataContainer
                .InterfaceImplementationEntries
                .OrderBy(entry => ECMA335.CodedIndex.TypeDefOrRef(entry.Type))
                .ThenBy(entry => ECMA335.CodedIndex.TypeDefOrRefOrSpec(entry.ImplementedInterface))
                .ToList()
                .ForEach(entry => metadataContainer.MetadataBuilder.AddInterfaceImplementation(entry.Type, entry.ImplementedInterface));

        public class InterfaceImplementationEntry
        {
            public readonly SRM.TypeDefinitionHandle Type;
            public readonly SRM.EntityHandle ImplementedInterface;

            public InterfaceImplementationEntry(SRM.TypeDefinitionHandle type, SRM.EntityHandle implementedInterface)
            {
                Type = type;
                ImplementedInterface = implementedInterface;
            }
        }
    }
}
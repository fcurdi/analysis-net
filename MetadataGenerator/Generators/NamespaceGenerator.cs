using System.Linq;
using Model;
using Model.Types;
using SRM = System.Reflection.Metadata;

namespace MetadataGenerator.Generators
{
    internal class NamespaceGenerator
    {
        private readonly MetadataContainer metadataContainer;
        private readonly TypeGenerator typeGenerator;

        public NamespaceGenerator(MetadataContainer metadataContainer)
        {
            this.metadataContainer = metadataContainer;
            typeGenerator = new TypeGenerator(metadataContainer);
        }

        public void Generate(Namespace namezpace)
        {
            foreach (var type in namezpace.Types)
            {
                GenerateTypes(type);
            }

            foreach (var nestedNamespace in namezpace.Namespaces)
            {
                Generate(nestedNamespace);
            }
        }

        private SRM.TypeDefinitionHandle GenerateTypes(TypeDefinition type)
        {
            var nestedTypes = type.Types.Select(GenerateTypes).ToList();

            var typeDefinitionHandle = typeGenerator.Generate(type);
            foreach (var nestedType in nestedTypes)
            {
                metadataContainer.RegisterNestedType(nestedType, typeDefinitionHandle);
            }

            return typeDefinitionHandle;
        }
    }
}
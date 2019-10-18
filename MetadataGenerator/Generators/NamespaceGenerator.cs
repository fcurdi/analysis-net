using System.Collections.Generic;
using Model;
using Model.Types;
using SRM = System.Reflection.Metadata;

namespace MetadataGenerator.Generators
{
    class NamespaceGenerator
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
            foreach (var nestedNamespace in namezpace.Namespaces)
            {
                Generate(nestedNamespace);
            }

            foreach (var type in namezpace.Types)
            {
                GenerateTypes(type);
            }
        }

        private SRM.TypeDefinitionHandle GenerateTypes(TypeDefinition type)
        {
            var nestedTypes = new List<SRM.TypeDefinitionHandle>();
            foreach (var nestedType in type.Types)
            {
                nestedTypes.Add(GenerateTypes(nestedType));
            }

            var typeDefinitionHandle = typeGenerator.Generate(type);
            foreach (var nestedType in nestedTypes)
            {
                metadataContainer.metadataBuilder.AddNestedType(nestedType, typeDefinitionHandle);
            }

            return typeDefinitionHandle;
        }

    }
}

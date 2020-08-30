using MetadataGenerator.Generation.Types;
using Assembly = Model.Assembly;
using SR = System.Reflection;

namespace MetadataGenerator.Generation
{
    internal static class AssemblyGenerator
    {
        public static MetadataContainer Generate(Assembly assembly)
        {
            var metadataContainer = new MetadataContainer(assembly);
            var namespaceGenerator = new NamespaceGenerator(metadataContainer);
            var moduleGenerator = new ModuleGenerator(metadataContainer);
            var metadataBuilder = metadataContainer.MetadataBuilder;

            metadataBuilder.AddAssembly(
                name: metadataBuilder.GetOrAddString(assembly.Name),
                version: assembly.Version,
                culture: metadataBuilder.GetOrAddString(assembly.Culture),
                publicKey: metadataBuilder.GetOrAddBlob(assembly.PublicKey),
                flags: SR.AssemblyFlags.PublicKey,
                hashAlgorithm: SR.AssemblyHashAlgorithm.Sha1
            );

            moduleGenerator.GenerateModuleFor(assembly);
            namespaceGenerator.Generate(assembly.RootNamespace);

            // Some tables must be sorted by one or more of their columns. Since the dll's methods and types don't follow a
            // particular order, the info needed to load this tables is stored during type/method generation but not added to the MetadataBuilder
            // until now where they can be previously sorted
            InterfaceImplementationGenerator.GenerateInterfaceImplementations(metadataContainer);
            GenericParameterGenerator.GenerateGenericParameters(metadataContainer);
            NestedTypeGenerator.GenerateNestedTypes(metadataContainer);

            return metadataContainer;
        }
    }
}
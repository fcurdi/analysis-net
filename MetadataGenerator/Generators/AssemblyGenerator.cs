using System;
using MetadataGenerator.Metadata;
using Model;
using SR = System.Reflection;

namespace MetadataGenerator.Generators
{
    internal static class AssemblyGenerator
    {
        public static MetadataContainer Generate(Assembly assembly)
        {
            var metadataContainer = new MetadataContainer(assembly);
            var namespaceGenerator = new NamespaceGenerator(metadataContainer);
            var metadataBuilder = metadataContainer.metadataBuilder;

            foreach (var namezpace in assembly.RootNamespace.Namespaces)
            {
                namespaceGenerator.Generate(namezpace);
            }

            metadataBuilder.AddAssembly(
                name: metadataBuilder.GetOrAddString(assembly.Name),
                version: new Version(0, 0, 0, 1),
                culture: default,
                publicKey: default,
                flags: SR.AssemblyFlags.PublicKey,
                hashAlgorithm: SR.AssemblyHashAlgorithm.Sha1
            );

            var moduleName = $"{assembly.Name}.{(metadataContainer.Executable ? "exe" : "dll")}";
            metadataBuilder.AddModule(
                generation: 0,
                moduleName: metadataBuilder.GetOrAddString(moduleName),
                mvid: metadataBuilder.GetOrAddGuid(Guid.NewGuid()),
                encId: default,
                encBaseId: default);

            Console.WriteLine($"Generating: {moduleName}");
            /*
             * Generic parameters table must be sorted by owner (TypeOrMethodDef that owns the generic parameter). Since the dll's methods and types don't follow a
             * particular order, the info needed to generate this parameters is stored during type/method generation but not added to the MetadataBuilder until now
            */
            metadataContainer.GenerateGenericParameters();

            // nested types table also needs to be sorted
            metadataContainer.GenerateNestedTypes();

            return metadataContainer;
        }
    }
}
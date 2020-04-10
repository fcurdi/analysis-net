using System;
using System.Reflection.Metadata.Ecma335;
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

            metadataBuilder.AddAssembly(
                name: metadataBuilder.GetOrAddString(assembly.Name),
                version: assembly.Version,
                culture: default,
                publicKey: default,
                flags: SR.AssemblyFlags.PublicKey,
                hashAlgorithm: SR.AssemblyHashAlgorithm.Sha1
            );

            var moduleName = $"{assembly.Name}.{(assembly.Kind == AssemblyKind.EXE ? "exe" : "dll")}";
            metadataContainer.ModuleHandle = metadataBuilder.AddModule(
                generation: 0,
                moduleName: metadataBuilder.GetOrAddString(moduleName),
                mvid: metadataBuilder.GetOrAddGuid(Guid.NewGuid()),
                encId: default,
                encBaseId: default);
            
            /*
             *  CLI defines a special class, named <Module>, that does not have a base type and does not implement any interfaces.
             * (This class is a toplevel class; i.e., it is not nested.). Used as owner of global members (methods, fields).
            */
            metadataBuilder.AddTypeDefinition(
                attributes: default,
                @namespace: default,
                name: metadataBuilder.GetOrAddString("<Module>"),
                baseType: default,
                fieldList: MetadataTokens.FieldDefinitionHandle(1),
                methodList: MetadataTokens.MethodDefinitionHandle(1));


            foreach (var namezpace in assembly.RootNamespace.Namespaces)
            {
                namespaceGenerator.Generate(namezpace);
            }

            /*
            * Some tables must be sorted by one or more of their columns. Since the dll's methods and types don't follow a
             * particular order, the info needed to load this tables is stored during type/method generation but not added to the MetadataBuilder until now
             * where they can be previously sorted
            */
            metadataContainer.GenerateInterfaceImplementations();
            metadataContainer.GenerateGenericParameters();
            metadataContainer.GenerateNestedTypes();

            return metadataContainer;
        }
    }
}
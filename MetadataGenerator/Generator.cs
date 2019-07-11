using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Model;
using Assembly = Model.Assembly;

namespace MetadataGenerator
{
    public class Generator : IGenerator
    {
        private static readonly Guid s_guid = new Guid("97F4DBD4-F6D1-4FAD-91B3-1001F92068E5"); //FIXME: ??
        private static readonly BlobContentId s_contentId = new BlobContentId(s_guid, 0x04030201); //FIXME: ??
        private readonly IDictionary<string, AssemblyReferenceHandle> assemblyReferences = new Dictionary<string, AssemblyReferenceHandle>();
        //FIXME better name. It's not all the type references but just the ones needed for Base type and interfaces
        private readonly IDictionary<string, TypeReferenceHandle> typeReferences = new Dictionary<string, TypeReferenceHandle>();

        public void Generate(Assembly assembly)
        {

            using (var peStream = File.OpenWrite($"./{assembly.Name}.dll"))
            {
                var metadata = new MetadataBuilder();

                metadata.AddAssembly(
                    name: metadata.GetOrAddString(assembly.Name),
                    version: new Version(1, 0, 0, 0), // FIXME ??
                    culture: default(StringHandle), // FIXME ??
                    publicKey: default(BlobHandle), // FIXME ??
                    flags: AssemblyFlags.PublicKey, // FIXME ??
                    hashAlgorithm: AssemblyHashAlgorithm.Sha1); // FIXME ??

                //FIXME see references in IlSpy generated vs original
                //FIXME: assemblyName => assemblyRef could result in false positive?
                foreach (var assemblyReference in assembly.References)
                {
                    assemblyReferences.Add(assemblyReference.Name, metadata.AddAssemblyReference(
                            name: metadata.GetOrAddString(assemblyReference.Name),
                            version: new Version(4, 0, 0, 0), //FIXME ??
                            culture: metadata.GetOrAddString("neutral"), //FIXME ??
                            publicKeyOrToken: metadata.GetOrAddBlob(ImmutableArray.Create<byte>(0xB7, 0x7A, 0x5C, 0x56, 0x19, 0x34, 0xE0, 0x89)),//FIXME ??
                            flags: default(AssemblyFlags), //FIXME ??
                            hashValue: default(BlobHandle))//FIXME ??
                    );
                }

                // FIXME parameters depend of assembly info that is not in the model?
                metadata.AddModule(
                    generation: 0, // FIXME ??
                    moduleName: metadata.GetOrAddString($"{assembly.Name}.dll"),
                    mvid: metadata.GetOrAddGuid(s_guid), // FIXME ?? module identified id
                    encId: default(GuidHandle), // FIXME ??
                    encBaseId: default(GuidHandle)); // FIXME ??

                var methodGenerator = new MethodGenerator(metadata);
                var fieldGenerator = new FieldGenerator(metadata);

                foreach (var namezpace in assembly.RootNamespace.Namespaces)
                {
                    foreach (var typeDefinition in namezpace.Types)
                    {
                        var typeDefinitionHandle = GenerateType(assembly, metadata, methodGenerator, fieldGenerator, typeDefinition);
                        foreach (var nestedType in typeDefinition.Types)
                        {
                            metadata.AddNestedType(
                                GenerateType(assembly, metadata, methodGenerator, fieldGenerator, nestedType),
                                typeDefinitionHandle);
                        }
                    }
                }
                var peHeaderBuilder = new PEHeaderBuilder(imageCharacteristics: Characteristics.Dll);
                var peBuilder = new ManagedPEBuilder(
                               header: peHeaderBuilder,
                               metadataRootBuilder: new MetadataRootBuilder(metadata),
                               ilStream: methodGenerator.IlStream(),
                               entryPoint: default(MethodDefinitionHandle),
                               flags: CorFlags.ILOnly | CorFlags.StrongNameSigned,
                               deterministicIdProvider: content => s_contentId);
                var peBlob = new BlobBuilder();
                var contentId = peBuilder.Serialize(peBlob);
                peBlob.WriteContentTo(peStream);
            }

        }

        private TypeDefinitionHandle GenerateType(Assembly assembly, MetadataBuilder metadata, MethodGenerator methodGenerator, FieldGenerator fieldGenerator, Model.Types.TypeDefinition typeDefinition)
        {
            MethodDefinitionHandle? firstMethodHandle = null;
            FieldDefinitionHandle? firstFieldHandle = null;

            var typeAttributes = AttributesProvider.GetAttributesFor(typeDefinition);

            foreach (var method in typeDefinition.Methods)
            {
                var methodHandle = methodGenerator.Generate(method);

                if (!firstMethodHandle.HasValue)
                {
                    firstMethodHandle = methodHandle;
                }
            }

            foreach (var field in typeDefinition.Fields)
            {
                var fieldDefinitionHandle = fieldGenerator.Generate(field);


                if (!firstFieldHandle.HasValue)
                {
                    firstFieldHandle = fieldDefinitionHandle;
                }
            }

            /* TODO Properties: works but model is missing Property concept

                var propertySignatureBlogBuilder = new BlobBuilder();
                new BlobEncoder(propertySignatureBlogBuilder)
                    .PropertySignature(isInstanceProperty: true) //FIXME when false
                    .Parameters(
                    0,
                    returnType => returnType.Type().Int32(), //FIXME backingField type
                    parameters => { });

                var propertyDefinitionHandle = metadata.AddProperty(
                    attributes: PropertyAttributes.None, //FIXME
                    name: metadata.GetOrAddString(""), //FIXME property name
                    signature: metadata.GetOrAddBlob(propertySignatureBlogBuilder));

                // asociate methods (get, set) to property  
                metadata.AddMethodSemantics(
                    propertyDefinitionHandle,
                    method.Name.StartsWith("get_") ? MethodSemanticsAttributes.Getter : MethodSemanticsAttributes.Setter, //FIXME,
                    methodHandle); //getter/setter
                metadata.AddPropertyMap(typeDefinitionHandle, propertyDefinitionHandle);
            */

            var baseType = default(EntityHandle);
            if (typeDefinition.Base != null)
            {
                baseType = GetOrAddTypeReference(assembly, metadata, typeDefinition.Base);
            }

            var typeDefinitionHandle = metadata.AddTypeDefinition(
                attributes: typeAttributes,
                @namespace: metadata.GetOrAddString(typeDefinition.ContainingNamespace.Name),
                name: metadata.GetOrAddString(typeDefinition.Name),
                baseType: baseType,
                fieldList: firstFieldHandle ?? fieldGenerator.NextFieldHandle(),
                methodList: firstMethodHandle ?? methodGenerator.NextMethodHandle());

            foreach (var interfaze in typeDefinition.Interfaces)
            {
                metadata.AddInterfaceImplementation(type: typeDefinitionHandle, implementedInterface: GetOrAddTypeReference(assembly, metadata, interfaze));
            }

            return typeDefinitionHandle;
        }


        private EntityHandle GetOrAddTypeReference(Assembly currentAssembly, MetadataBuilder metadata, Model.Types.IBasicType type)
        {
            TypeReferenceHandle typeReference;
            var typeReferenceKey = $"{type.ContainingAssembly.Name}.{type.ContainingNamespace}.{type.Name}";
            if (typeReferences.TryGetValue(typeReferenceKey, out var value))
            {
                typeReference = value;
            }
            else
            {
                var resolutionScope = type.ContainingAssembly.Name.Equals(currentAssembly.Name)
                    ? default(AssemblyReferenceHandle)
                    : assemblyReferences[type.ContainingAssembly.Name];

                //FIXME: comparing to the name of the current assembly could result in a false positive?
                typeReference = metadata.AddTypeReference(
                    resolutionScope: resolutionScope,
                    @namespace: metadata.GetOrAddString(type.ContainingNamespace),
                    name: metadata.GetOrAddString(type.Name));

                typeReferences.Add(typeReferenceKey, typeReference);

            }

            return typeReference;
        }

    }
}
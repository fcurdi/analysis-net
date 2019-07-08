using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
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

                metadata.AddModule(
                    generation: 0, // FIXME ??
                    moduleName: metadata.GetOrAddString($"{assembly.Name}.dll"),
                    mvid: metadata.GetOrAddGuid(s_guid), // FIXME ??
                    encId: default(GuidHandle), // FIXME ??
                    encBaseId: default(GuidHandle)); // FIXME ??

                var ilBuilder = new BlobBuilder();
                var methodBodyStream = new MethodBodyStreamEncoder(ilBuilder);
                var metadataTokensMethodsOffset = 1;
                var metadataTokensFieldsOffset = 1;
                var methodGenerator = new MethodGenerator(metadata, ref methodBodyStream);

                foreach (var namezpace in assembly.RootNamespace.Namespaces)
                {
                    foreach (var typeDefinition in namezpace.Types)
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
                            metadataTokensMethodsOffset++;
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

                        if (typeDefinition.Kind.Equals(Model.Types.TypeDefinitionKind.Class))
                        {
                            //TODO metadata.AddNestedType() if applies


                            // Field initial values are assigned either on ctor or cctor (if static)
                            typeDefinition.Fields //TODO extract method? duplicated code for enum
                                .ToList()
                                .ForEach(field =>
                                {
                                    var fieldSignatureBlobBuilder = new BlobBuilder();
                                    TypeEncoder.Encode(
                                         field.Type,
                                         new BlobEncoder(fieldSignatureBlobBuilder)
                                         .FieldSignature());

                                    var fieldDefinitionHandle = metadata.AddFieldDefinition(
                                        attributes: AttributesProvider.GetAttributesFor(field),
                                        name: metadata.GetOrAddString(field.Name),
                                        signature: metadata.GetOrAddBlob(fieldSignatureBlobBuilder));

                                    if (!firstFieldHandle.HasValue)
                                    {
                                        firstFieldHandle = fieldDefinitionHandle;
                                    }

                                    metadataTokensFieldsOffset++;
                                });

                            var typeDefinitionHandle = metadata.AddTypeDefinition(
                                attributes: typeAttributes,
                                @namespace: metadata.GetOrAddString(namezpace.Name),
                                name: metadata.GetOrAddString(typeDefinition.Name),
                                baseType: typeDefinition.Base == null ? default(EntityHandle) : BaseType(assembly, metadata, typeDefinition),
                                fieldList: firstFieldHandle ?? MetadataTokens.FieldDefinitionHandle(metadataTokensFieldsOffset),
                                methodList: firstMethodHandle ?? MetadataTokens.MethodDefinitionHandle(metadataTokensMethodsOffset));

                            foreach (var interfaze in typeDefinition.Interfaces)
                            {
                                metadata.AddInterfaceImplementation(
                                    type: typeDefinitionHandle,
                                    implementedInterface: metadata.AddTypeReference( //FIXME multiple classes could implement same interface
                                                                                     //FIXME so should addTypeReference only once. check MetadataTokens for reference?
                                        resolutionScope: default(TypeReferenceHandle),
                                        @namespace: metadata.GetOrAddString(interfaze.ContainingNamespace),
                                        name: metadata.GetOrAddString(interfaze.Name)));
                            }


                        }
                        else if (typeDefinition.Kind.Equals(Model.Types.TypeDefinitionKind.Enum))
                        {
                            var fieldSignatureBlobBuilder = new BlobBuilder();
                            TypeEncoder.Encode(
                                typeDefinition.Fields.First().Type, //FIXME first if empty
                                new BlobEncoder(fieldSignatureBlobBuilder)
                                .FieldSignature());

                            metadataTokensFieldsOffset++;

                            firstFieldHandle = metadata.AddFieldDefinition(
                                attributes: FieldAttributes.Public | FieldAttributes.SpecialName | FieldAttributes.RTSpecialName,
                                name: metadata.GetOrAddString("value__"),
                                signature: metadata.GetOrAddBlob(fieldSignatureBlobBuilder));

                            var selfTypeDefinitionHandle = metadata.AddTypeDefinition(
                                attributes: typeAttributes,
                                @namespace: metadata.GetOrAddString(namezpace.Name),
                                name: metadata.GetOrAddString(typeDefinition.Name),
                                baseType: typeDefinition.Base == null ? default(EntityHandle) : BaseType(assembly, metadata, typeDefinition),
                                fieldList: firstFieldHandle ?? MetadataTokens.FieldDefinitionHandle(metadataTokensFieldsOffset),
                                methodList: firstMethodHandle ?? MetadataTokens.MethodDefinitionHandle(metadataTokensMethodsOffset));

                            typeDefinition.Fields
                                .Where(field => !field.Name.Equals("value__"))
                                .ToList()
                                .ForEach(field =>
                                {
                                    fieldSignatureBlobBuilder.Clear();
                                    new BlobEncoder(fieldSignatureBlobBuilder)
                                        .FieldSignature()
                                        .Type(selfTypeDefinitionHandle, true);

                                    metadata.AddConstant(
                                        metadata.AddFieldDefinition(
                                            attributes: AttributesProvider.GetAttributesFor(field),
                                            name: metadata.GetOrAddString(field.Name),
                                            signature: metadata.GetOrAddBlob(fieldSignatureBlobBuilder)),
                                        field.Value.Value);

                                    metadataTokensFieldsOffset++;
                                });
                        }
                        else if (typeDefinition.Kind.Equals(Model.Types.TypeDefinitionKind.Interface))
                        {
                            metadata.AddTypeDefinition(
                                attributes: typeAttributes,
                                @namespace: metadata.GetOrAddString(namezpace.Name),
                                name: metadata.GetOrAddString(typeDefinition.Name),
                                baseType: typeDefinition.Base == null ? default(EntityHandle) : BaseType(assembly, metadata, typeDefinition),
                                fieldList: firstFieldHandle ?? MetadataTokens.FieldDefinitionHandle(metadataTokensFieldsOffset),
                                methodList: firstMethodHandle ?? MetadataTokens.MethodDefinitionHandle(metadataTokensMethodsOffset));
                        }

                    }
                }
                var peHeaderBuilder = new PEHeaderBuilder(imageCharacteristics: Characteristics.Dll);
                var peBuilder = new ManagedPEBuilder(
                               header: peHeaderBuilder,
                               metadataRootBuilder: new MetadataRootBuilder(metadata),
                               ilStream: ilBuilder,
                               entryPoint: default(MethodDefinitionHandle),
                               flags: CorFlags.ILOnly | CorFlags.StrongNameSigned,
                               deterministicIdProvider: content => s_contentId);
                var peBlob = new BlobBuilder();
                var contentId = peBuilder.Serialize(peBlob);
                peBlob.WriteContentTo(peStream);
            }

        }

        //FIXME name
        //FIXME: comparing to the name of the current assembly could result in a false positive?
        //FIXME: this posibly adds the type reference more than once
        private TypeReferenceHandle BaseType(Assembly assembly, MetadataBuilder metadata, Model.Types.TypeDefinition typeDefinition)
        {
            return metadata.AddTypeReference(
                resolutionScope: typeDefinition.Base.ContainingAssembly.Name.Equals(assembly.Name) ? default(AssemblyReferenceHandle) : assemblyReferences[typeDefinition.Base.ContainingAssembly.Name],
                @namespace: metadata.GetOrAddString(typeDefinition.Base.ContainingNamespace),
                name: metadata.GetOrAddString(typeDefinition.Base.Name));
        }


    }
}
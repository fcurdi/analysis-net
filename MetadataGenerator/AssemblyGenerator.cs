using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Model;

namespace MetadataGenerator
{
    public class AssemblyGenerator
    {
        private readonly Model.Assembly assembly;
        private readonly MetadataBuilder metadata;
        private readonly TypeReferences typeReferences;
        private readonly MethodGenerator methodGenerator;
        private readonly FieldGenerator fieldGenerator;
        private MetadataBuilder generatedMetadata = null;

        public MetadataBuilder GeneratedMetadata => generatedMetadata ?? throw new Exception("Generate was not called");
        public BlobBuilder IlStream => generatedMetadata != null ? methodGenerator.IlStream() : throw new Exception("Generate was not called");

        private AssemblyGenerator(Model.Assembly assembly, MetadataBuilder metadata, TypeReferences typeReferences, MethodGenerator methodGenerator, FieldGenerator fieldGenerator)
        {
            this.metadata = metadata;
            this.typeReferences = typeReferences;
            this.methodGenerator = methodGenerator;
            this.fieldGenerator = fieldGenerator;
            this.assembly = assembly;
        }

        public static AssemblyGenerator For(Model.Assembly assembly)
        {
            var metadata = new MetadataBuilder();
            var typeReferences = new TypeReferences(metadata, assembly);
            var typeEncoder = new TypeEncoder(typeReferences);
            var methodGenerator = new MethodGenerator(metadata, typeEncoder);
            var fieldGenerator = new FieldGenerator(metadata, typeEncoder);
            return new AssemblyGenerator(
                assembly,
                metadata,
                typeReferences,
                methodGenerator,
                fieldGenerator);
        }


        public AssemblyGenerator Generate()
        {
            if (generatedMetadata != null)
            {
                throw new Exception("Generate was already called for this generator");
            }

            // FIXME parameters depend of assembly info that is not in the model?
            metadata.AddAssembly(
                name: metadata.GetOrAddString(assembly.Name),
                version: new Version(1, 0, 0, 0), // FIXME ??
                culture: default(StringHandle), // FIXME ??
                publicKey: default(BlobHandle), // FIXME ??
                flags: AssemblyFlags.PublicKey, // FIXME ??
                hashAlgorithm: AssemblyHashAlgorithm.Sha1); // FIXME ??

            // FIXME parameters depend of assembly info that is not in the model?
            metadata.AddModule(
                generation: 0, // FIXME ??
                moduleName: metadata.GetOrAddString($"{assembly.Name}.dll"),
                mvid: default(GuidHandle), // FIXME ??
                encId: default(GuidHandle), // FIXME ??
                encBaseId: default(GuidHandle)); // FIXME ??

            foreach (var namezpace in assembly.RootNamespace.Namespaces)
            {
                Generate(namezpace);
            }

            generatedMetadata = metadata;

            return this;
        }

        private void Generate(Namespace namezpace)
        {
            foreach (var nestedNamespace in namezpace.Namespaces)
            {
                Generate(nestedNamespace);
            }

            foreach (var type in namezpace.Types)
            {
                GenerateTypesOf(type);
            }
        }

        private TypeDefinitionHandle GenerateTypesOf(Model.Types.TypeDefinition type)
        {
            var nestedTypes = new List<TypeDefinitionHandle>();
            foreach (var nestedType in type.Types)
            {
                nestedTypes.Add(GenerateTypesOf(nestedType));
            }

            var typeDefinitionHandle = Generate(type);
            foreach (var nestedType in nestedTypes)
            {
                metadata.AddNestedType(nestedType, typeDefinitionHandle);
            }

            return typeDefinitionHandle;
        }

        private TypeDefinitionHandle Generate(Model.Types.TypeDefinition type)
        {
            MethodDefinitionHandle? firstMethodHandle = null;
            FieldDefinitionHandle? firstFieldHandle = null;

            var typeAttributes = AttributesProvider.GetAttributesFor(type);

            foreach (var method in type.Methods)
            {
                var methodHandle = methodGenerator.Generate(method);

                if (!firstMethodHandle.HasValue)
                {
                    firstMethodHandle = methodHandle;
                }
            }

            foreach (var field in type.Fields)
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
            if (type.Base != null)
            {
                baseType = typeReferences.TypeReferenceOf(type.Base);
            }

            var typeDefinitionHandle = metadata.AddTypeDefinition(
                attributes: typeAttributes,
                @namespace: metadata.GetOrAddString(type.ContainingNamespace.FullName),
                name: metadata.GetOrAddString(type.Name),
                baseType: baseType,
                fieldList: firstFieldHandle ?? fieldGenerator.NextFieldHandle(),
                methodList: firstMethodHandle ?? methodGenerator.NextMethodHandle());

            foreach (var interfaze in type.Interfaces)
            {
                metadata.AddInterfaceImplementation(type: typeDefinitionHandle, implementedInterface: typeReferences.TypeReferenceOf(interfaze));
            }

            return typeDefinitionHandle;
        }
    }
}

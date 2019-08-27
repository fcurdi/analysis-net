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
        private MetadataBuilder generatedMetadata;

        private MethodDefinitionHandle? mainMethodHandle;
        public MethodDefinitionHandle? MainMethodHandle => generatedMetadata != null ? mainMethodHandle : throw new Exception("Generate was not called");
        public bool Executable => mainMethodHandle != null;

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

            foreach (var namezpace in assembly.RootNamespace.Namespaces)
            {
                Generate(namezpace);
            }

            // FIXME args
            metadata.AddAssembly(
                name: metadata.GetOrAddString(assembly.Name),
                version: new Version(1, 0, 0, 0),
                culture: default(StringHandle),
                publicKey: default(BlobHandle),
                flags: AssemblyFlags.PublicKey,
                hashAlgorithm: AssemblyHashAlgorithm.Sha1);

            metadata.AddModule(
                    generation: 0,
                    moduleName: metadata.GetOrAddString($"{assembly.Name}.{(Executable ? "exe" : "dll")}"),
                    mvid: metadata.GetOrAddGuid(Guid.NewGuid()),
                    encId: metadata.GetOrAddGuid(Guid.Empty),
                    encBaseId: metadata.GetOrAddGuid(Guid.Empty));

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
            var fieldDefinitionHandles = new List<FieldDefinitionHandle>();
            var methodDefinitionHandles = new List<MethodDefinitionHandle>();

            foreach (var field in type.Fields)
            {
                fieldDefinitionHandles.Add(fieldGenerator.Generate(field));
            }

            /* TODO Properties: (works) but model is missing Property concept

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


            foreach (var method in type.Methods)
            {
                var methodHandle = methodGenerator.Generate(method);

                methodDefinitionHandles.Add(methodHandle);

                if (method.Name.Equals("Main")) // FIXME can a non entry point be called Main?
                {
                    mainMethodHandle = methodHandle;
                }
            }

            var baseType = default(EntityHandle);
            if (type.Base != null)
            {
                baseType = typeReferences.TypeReferenceOf(type.Base);
            }
            var typeDefinitionHandle = metadata.AddTypeDefinition(
                attributes: AttributesProvider.GetTypeAttributesFor(type),
                @namespace: metadata.GetOrAddString(type.ContainingNamespace.FullName),
                name: metadata.GetOrAddString(type.Name),
                baseType: baseType,
                fieldList: fieldDefinitionHandles.FirstOr(fieldGenerator.NextFieldHandle()),
                methodList: methodDefinitionHandles.FirstOr(methodGenerator.NextMethodHandle()));

            foreach (var interfaze in type.Interfaces)
            {
                metadata.AddInterfaceImplementation(type: typeDefinitionHandle, implementedInterface: typeReferences.TypeReferenceOf(interfaze));
            }

            /*
               Generic parameters table must be sorted that's why this is done at the end and not during the method generation.
               If done that way, method generic parameters of a type are added before the type's generic parameters and table results
               unsorted
             */


            // generate class generic parameters (Class<T>)
            foreach (var genericParamter in type.GenericParameters)
            {
                metadata.AddGenericParameter(
                    typeDefinitionHandle,
                    GenericParameterAttributes.None,
                    metadata.GetOrAddString(genericParamter.Name),
                    genericParamter.Index);
            }

            // generate method generic parameters (public T method<T>(T param))
            for (int i = 0; i < type.Methods.Count; i++)
            {
                var method = type.Methods[i];
                foreach (var genericParameter in method.GenericParameters)
                {
                    metadata.AddGenericParameter(
                        methodDefinitionHandles[i],
                        GenericParameterAttributes.None,
                        metadata.GetOrAddString(genericParameter.Name),
                        genericParameter.Index);
                }
            }

            return typeDefinitionHandle;
        }
    }
}



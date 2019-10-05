using System;
using System.Collections.Generic;
using Model;
using Model.Types;
using static MetadataGenerator.AttributesProvider;
using ECMA335 = System.Reflection.Metadata.Ecma335;
using SR = System.Reflection;
using SRM = System.Reflection.Metadata;

namespace MetadataGenerator
{
    public class AssemblyGenerator
    {
        private readonly Assembly assembly;
        private readonly ECMA335.MetadataBuilder metadata;
        private readonly ReferencesProvider referencesProvider;
        private readonly MethodGenerator methodGenerator;
        private readonly FieldGenerator fieldGenerator;

        private T CheckMetadataWasGeneratedAndReturn<T>(T value) => generatedMetadata != null ? value : throw new Exception("Generate was not called");
        private ECMA335.MetadataBuilder generatedMetadata;
        public ECMA335.MetadataBuilder GeneratedMetadata { get => CheckMetadataWasGeneratedAndReturn(generatedMetadata); }
        private SRM.MethodDefinitionHandle? mainMethodHandle;
        public SRM.MethodDefinitionHandle? MainMethodHandle { get => CheckMetadataWasGeneratedAndReturn(mainMethodHandle); }
        public bool Executable => mainMethodHandle != null;
        public SRM.BlobBuilder IlStream => CheckMetadataWasGeneratedAndReturn(methodGenerator.IlStream());

        private AssemblyGenerator(Assembly assembly, ECMA335.MetadataBuilder metadata, ReferencesProvider referencesProvider, MethodGenerator methodGenerator, FieldGenerator fieldGenerator)
        {
            this.metadata = metadata;
            this.referencesProvider = referencesProvider;
            this.methodGenerator = methodGenerator;
            this.fieldGenerator = fieldGenerator;
            this.assembly = assembly;
        }

        public static AssemblyGenerator For(Assembly assembly)
        {
            var metadata = new ECMA335.MetadataBuilder();
            var referencesProvider = new ReferencesProvider(metadata, assembly);
            var typeEncoder = new TypeEncoder(referencesProvider);
            var methodGenerator = new MethodGenerator(metadata, typeEncoder, referencesProvider);
            var fieldGenerator = new FieldGenerator(metadata, typeEncoder);
            return new AssemblyGenerator(assembly, metadata, referencesProvider, methodGenerator, fieldGenerator);
        }

        public AssemblyGenerator Generate()
        {
            if (generatedMetadata != null) throw new Exception("Generate was already called for this generator");

            foreach (var namezpace in assembly.RootNamespace.Namespaces)
            {
                Generate(namezpace);
            }

            // FIXME args
            metadata.AddAssembly(
                name: metadata.GetOrAddString(assembly.Name),
                version: new Version(1, 0, 0, 0),
                culture: default,
                publicKey: default,
                flags: SR.AssemblyFlags.PublicKey,
                hashAlgorithm: SR.AssemblyHashAlgorithm.Sha1);

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

            var typeDefinitionHandle = Generate(type);
            foreach (var nestedType in nestedTypes)
            {
                metadata.AddNestedType(nestedType, typeDefinitionHandle);
            }

            return typeDefinitionHandle;
        }

        private SRM.TypeDefinitionHandle Generate(TypeDefinition type)
        {
            var fieldDefinitionHandles = new List<SRM.FieldDefinitionHandle>();
            var methodDefinitionHandles = new List<SRM.MethodDefinitionHandle>();

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
                if (method.Name.Equals("Main"))
                {
                    mainMethodHandle = methodHandle;
                }
            }

            var typeDefinitionHandle = metadata.AddTypeDefinition(
                attributes: GetTypeAttributesFor(type),
                @namespace: metadata.GetOrAddString(type.ContainingNamespace.FullName),
                name: metadata.GetOrAddString(type.Name),
                baseType: type.Base != null ? referencesProvider.TypeReferenceOf(type.Base) : default,
                fieldList: fieldDefinitionHandles.FirstOr(ECMA335.MetadataTokens.FieldDefinitionHandle(metadata.NextRowFor(ECMA335.TableIndex.Field))),
                methodList: methodDefinitionHandles.FirstOr(ECMA335.MetadataTokens.MethodDefinitionHandle(metadata.NextRowFor(ECMA335.TableIndex.MethodDef))));

            foreach (var interfaze in type.Interfaces)
            {
                metadata.AddInterfaceImplementation(type: typeDefinitionHandle, implementedInterface: referencesProvider.TypeReferenceOf(interfaze));
            }

            /*
               Generic parameters table must be sorted that's why this is done at the end and not during the method generation.
               If done that way, method generic parameters of a type are added before the type's generic parameters and table results unsorted
             */

            // generate class generic parameters (Class<T>)
            foreach (var genericParamter in type.GenericParameters)
            {
                metadata.AddGenericParameter(
                    typeDefinitionHandle,
                    SR.GenericParameterAttributes.None,
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
                        SR.GenericParameterAttributes.None,
                        metadata.GetOrAddString(genericParameter.Name),
                        genericParameter.Index);
                }
            }

            return typeDefinitionHandle;
        }
    }
}



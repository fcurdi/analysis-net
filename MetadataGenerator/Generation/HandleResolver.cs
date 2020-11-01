using System;
using System.Collections.Generic;
using System.Reflection;
using Model;
using Model.ThreeAddressCode.Values;
using Model.Types;
using static System.Linq.Enumerable;
using static MetadataGenerator.Generation.Types.TypeGenerator;
using Assembly = Model.Assembly;
using ECMA335 = System.Reflection.Metadata.Ecma335;
using SRM = System.Reflection.Metadata;

namespace MetadataGenerator.Generation
{
    // Responsible providing handles of items (types, methods, fields, etc) needed when adding metadata definitions or when 
    // encoding method body instructions. Since generation is done in a single pass, definitions might no have been added yet. So this
    // module provides on demand handles in the form of references or specifications, which are added to the Metadata tables.
    //
    // This handles are stored for several reasons:
    //  - So they can be reused
    //  - To reduce generated metadata size
    //  - Because some tables do not allow duplicates
    // They are stored with some key that relates to the key in the corresponding table (see ECMA). This is useful for when 
    // a new one needs to be added and also to guarantee the uniqueness of the key.
    //
    // Also stores some handles like the one of the module definition and main method that are needed in the last step of the
    // generation.
    internal class HandleResolver
    {
        private readonly Assembly assembly;
        private readonly MetadataContainer metadataContainer;

        #region handles

        // module
        private SRM.ModuleDefinitionHandle? moduleHandle;

        public SRM.ModuleDefinitionHandle ModuleHandle
        {
            get => moduleHandle ?? throw new Exception("Module handle was not set");
            set
            {
                if (moduleHandle != null) throw new Exception("Multiple modules not supported");
                moduleHandle = value;
            }
        }
        //

        // assembly
        private readonly IDictionary<Tuple<SRM.StringHandle, Version, SRM.StringHandle, SRM.BlobHandle, AssemblyFlags, SRM.BlobHandle>,
            SRM.AssemblyReferenceHandle> assemblyRefHandles;

        private Tuple<SRM.StringHandle, Version, SRM.StringHandle, SRM.BlobHandle, AssemblyFlags, SRM.BlobHandle>
            RefKeyFor(IAssemblyReference assemblyReference)
        {
            var matchedAssemblyReference = assembly.References.First(assembly => assembly.Name == assemblyReference.Name);
            var name = metadataContainer.MetadataBuilder.GetOrAddString(matchedAssemblyReference.Name);
            var version = matchedAssemblyReference.Version;
            var culture = metadataContainer.MetadataBuilder.GetOrAddString(matchedAssemblyReference.Culture);
            var publicKey = metadataContainer.MetadataBuilder.GetOrAddBlob(matchedAssemblyReference.PublicKey);
            AssemblyFlags flags = default;
            SRM.BlobHandle hashValue = default;
            return new Tuple<SRM.StringHandle, Version, SRM.StringHandle, SRM.BlobHandle, AssemblyFlags, SRM.BlobHandle>(
                name,
                version,
                culture,
                publicKey,
                flags,
                hashValue
            );
        }
        //

        // type
        private readonly IDictionary<Tuple<SRM.EntityHandle, SRM.StringHandle, SRM.StringHandle>, SRM.TypeReferenceHandle> typeRefHandles;

        private Tuple<SRM.EntityHandle, SRM.StringHandle, SRM.StringHandle> RefKeyFor(IBasicType type)
        {
            SRM.EntityHandle resolutionScope;
            if (type.ContainingType == null)
            {
                resolutionScope = type.ContainingAssembly.Name.Equals(assembly.Name)
                    ? ModuleHandle
                    : (SRM.EntityHandle) GetOrAddAssemblyReference(type.ContainingAssembly);
            }
            else
            {
                resolutionScope = GetOrAddTypeReference(type.ContainingType);
            }

            var key = new Tuple<SRM.EntityHandle, SRM.StringHandle, SRM.StringHandle>(
                resolutionScope,
                metadataContainer.MetadataBuilder.GetOrAddString(type.ContainingNamespace),
                metadataContainer.MetadataBuilder.GetOrAddString(TypeNameOf(type))
            );
            return key;
        }

        private readonly IDictionary<SRM.BlobHandle, SRM.TypeSpecificationHandle> typeSpecHandles;

        private SRM.BlobHandle SpecKeyFor(IType type)
        {
            var signature = metadataContainer
                .Encoders
                .TypeSignatureEncoder
                .EncodeSignatureOf(type);

            var blobHandle = metadataContainer.MetadataBuilder.GetOrAddBlob(signature);
            var key = blobHandle;
            return key;
        }
        //

        //field
        private readonly IDictionary<Tuple<SRM.EntityHandle, SRM.StringHandle, SRM.BlobHandle>, SRM.MemberReferenceHandle> fieldRefHandles;

        private Tuple<SRM.EntityHandle, SRM.StringHandle, SRM.BlobHandle> RefKeyFor(IFieldReference field)
        {
            var signature = metadataContainer
                .Encoders
                .FieldSignatureEncoder
                .EncodeSignatureOf(field);

            var blobHandle = metadataContainer.MetadataBuilder.GetOrAddBlob(signature);
            var parent = HandleOf(field.ContainingType);
            var key = new Tuple<SRM.EntityHandle, SRM.StringHandle, SRM.BlobHandle>(
                parent,
                metadataContainer.MetadataBuilder.GetOrAddString(field.Name),
                blobHandle
            );
            return key;
        }

        //

        // method
        private readonly IDictionary<Tuple<SRM.EntityHandle, SRM.StringHandle, SRM.BlobHandle>, SRM.MemberReferenceHandle> methodRefHandles;

        private Tuple<SRM.EntityHandle, SRM.StringHandle, SRM.BlobHandle> RefKeyFor(IMethodReference method)
        {
            var signature = metadataContainer
                .Encoders
                .MethodSignatureEncoder
                .EncodeSignatureOf(method);

            var blobHandle = metadataContainer.MetadataBuilder.GetOrAddBlob(signature);
            var parent =
                method.ContainingType is ArrayTypeWrapper arrayTypeWrapper
                    ? HandleOf(arrayTypeWrapper.Type)
                    : HandleOf(method.ContainingType);
            var key = new Tuple<SRM.EntityHandle, SRM.StringHandle, SRM.BlobHandle>(
                parent,
                metadataContainer.MetadataBuilder.GetOrAddString(method.Name),
                blobHandle
            );
            return key;
        }

        private readonly IDictionary<Tuple<SRM.EntityHandle, SRM.BlobHandle>, SRM.MethodSpecificationHandle> methodSpecHandles;

        private Tuple<SRM.EntityHandle, SRM.BlobHandle> SpecKeyFor(IMethodReference method)
        {
            var signature = metadataContainer
                .Encoders
                .MethodSignatureEncoder
                .EncodeSignatureOf(method);

            var blobHandle = metadataContainer.MetadataBuilder.GetOrAddBlob(signature);
            var genericMethodHandle = HandleOf(method.GenericMethod);
            var key = new Tuple<SRM.EntityHandle, SRM.BlobHandle>(genericMethodHandle, blobHandle);
            return key;
        }

        private SRM.MethodDefinitionHandle? mainMethodHandle;

        public SRM.MethodDefinitionHandle MainMethodHandle
        {
            get => mainMethodHandle ?? throw new Exception("Main method handle was not set");
            set
            {
                if (mainMethodHandle != null) throw new Exception("Main method was already set");
                mainMethodHandle = value;
            }
        }
        //

        // other
        private readonly IDictionary<SRM.BlobHandle, SRM.StandaloneSignatureHandle> variablesSignatureHandles;

        private SRM.BlobHandle StandaloneSigKeyFor(IList<IVariable> localVariables)
        {
            var signature = metadataContainer
                .Encoders
                .MethodLocalsSignatureEncoder
                .EncodeSignatureOf(localVariables);

            var blobHandle = metadataContainer.MetadataBuilder.GetOrAddBlob(signature);
            var key = blobHandle;
            return key;
        }

        //

        #endregion


        public HandleResolver(MetadataContainer metadataContainer, Assembly assembly)
        {
            this.metadataContainer = metadataContainer;
            this.assembly = assembly;
            assemblyRefHandles =
                new Dictionary<Tuple<SRM.StringHandle, Version, SRM.StringHandle, SRM.BlobHandle, AssemblyFlags, SRM.BlobHandle>,
                    SRM.AssemblyReferenceHandle>();
            variablesSignatureHandles = new Dictionary<SRM.BlobHandle, SRM.StandaloneSignatureHandle>();
            variablesSignatureHandles = new Dictionary<SRM.BlobHandle, SRM.StandaloneSignatureHandle>();
            methodSpecHandles = new Dictionary<Tuple<SRM.EntityHandle, SRM.BlobHandle>, SRM.MethodSpecificationHandle>();
            typeSpecHandles = new Dictionary<SRM.BlobHandle, SRM.TypeSpecificationHandle>();
            fieldRefHandles = new Dictionary<Tuple<SRM.EntityHandle, SRM.StringHandle, SRM.BlobHandle>, SRM.MemberReferenceHandle>();
            methodRefHandles = new Dictionary<Tuple<SRM.EntityHandle, SRM.StringHandle, SRM.BlobHandle>, SRM.MemberReferenceHandle>();
            typeRefHandles = new Dictionary<Tuple<SRM.EntityHandle, SRM.StringHandle, SRM.StringHandle>, SRM.TypeReferenceHandle>();
        }

        public SRM.UserStringHandle UserStringHandleOf(string value) => metadataContainer.MetadataBuilder.GetOrAddUserString(value);

        public SRM.StandaloneSignatureHandle HandleOf(IList<IVariable> localVariables)
        {
            var key = StandaloneSigKeyFor(localVariables);
            if (!variablesSignatureHandles.TryGetValue(key, out var variableSignatureHandle))
            {
                // StandAloneSig Table (0x11)
                variableSignatureHandle = metadataContainer.MetadataBuilder.AddStandaloneSignature(signature: key);
                variablesSignatureHandles[key] = variableSignatureHandle;
            }

            return variableSignatureHandle;
        }

        public SRM.EntityHandle HandleOf(IMetadataReference metadataReference)
        {
            switch (metadataReference)
            {
                case IFieldReference field:
                    return GetOrAddFieldReference(field);
                case IMethodReference method:
                    return method.IsGenericInstantiation()
                        ? GetOrAddMethodSpecification(method)
                        : GetOrAddMethodReference(method);
                case IType type:
                    switch (type)
                    {
                        case IType iType when iType is ArrayType
                                              || iType is PointerType
                                              || iType is IGenericParameterReference
                                              || iType is IBasicType basicType && basicType.IsGenericInstantiation():
                            return GetOrAddTypeSpecification(iType);
                        case IBasicType basicType:
                            return GetOrAddTypeReference(basicType);
                        default:
                            throw new Exception($"type {type} not yet supported");
                    }
                default:
                    throw new Exception($"Metadata {metadataReference} reference not supported");
            }
        }

        private SRM.AssemblyReferenceHandle GetOrAddAssemblyReference(IAssemblyReference assemblyReference)
        {
            var key = RefKeyFor(assemblyReference);
            if (!assemblyRefHandles.TryGetValue(key, out var assemblyRefHandle))
            {
                // AssemblyRef Table (0x23) 
                assemblyRefHandle = metadataContainer.MetadataBuilder.AddAssemblyReference(
                    name: key.Item1,
                    version: key.Item2,
                    culture: key.Item3,
                    publicKeyOrToken: key.Item4,
                    flags: key.Item5,
                    hashValue: key.Item6
                );
                assemblyRefHandles.Add(key, assemblyRefHandle);
            }

            return assemblyRefHandle;
        }

        private SRM.EntityHandle GetOrAddTypeReference(IBasicType type)
        {
            var key = RefKeyFor(type);
            if (!typeRefHandles.TryGetValue(key, out var typeRefHandle))
            {
                // TypeRef Table (0x01) 
                typeRefHandle = metadataContainer.MetadataBuilder.AddTypeReference(
                    resolutionScope: key.Item1,
                    @namespace: key.Item2,
                    name: key.Item3);
                typeRefHandles.Add(key, typeRefHandle);
            }

            return typeRefHandle;
        }

        private SRM.TypeSpecificationHandle GetOrAddTypeSpecification(IType type)
        {
            var key = SpecKeyFor(type);
            if (!typeSpecHandles.TryGetValue(key, out var typeSpecHandle))
            {
                // TypeSpec Table (0x1B) 
                typeSpecHandle = metadataContainer.MetadataBuilder.AddTypeSpecification(signature: key);
                typeSpecHandles.Add(key, typeSpecHandle);
            }

            return typeSpecHandle;
        }

        private SRM.MethodSpecificationHandle GetOrAddMethodSpecification(IMethodReference method)
        {
            var key = SpecKeyFor(method);
            if (!methodSpecHandles.TryGetValue(key, out var methodSpecHandle))
            {
                // MethodSpec Table (0x2B) 
                methodSpecHandle = metadataContainer.MetadataBuilder.AddMethodSpecification(
                    method: key.Item1,
                    instantiation: key.Item2);
                methodSpecHandles.Add(key, methodSpecHandle);
            }

            return methodSpecHandle;
        }

        private SRM.EntityHandle GetOrAddMethodReference(IMethodReference method)
        {
            var key = RefKeyFor(method);
            if (!methodRefHandles.TryGetValue(key, out var methodRefHandle))
            {
                // MemberRef Table (0x0A)
                methodRefHandle = metadataContainer.MetadataBuilder.AddMemberReference(
                    parent: key.Item1,
                    name: key.Item2,
                    signature: key.Item3);
                methodRefHandles.Add(key, methodRefHandle);
            }

            return methodRefHandle;
        }

        private SRM.MemberReferenceHandle GetOrAddFieldReference(IFieldReference field)
        {
            var key = RefKeyFor(field);
            if (!fieldRefHandles.TryGetValue(key, out var fieldRefHandle))
            {
                // MemberRef Table (0x0A)
                fieldRefHandle = metadataContainer.MetadataBuilder.AddMemberReference(
                    parent: key.Item1,
                    name: key.Item2,
                    signature: key.Item3);
                fieldRefHandles.Add(key, fieldRefHandle);
            }

            return fieldRefHandle;
        }
    }
}
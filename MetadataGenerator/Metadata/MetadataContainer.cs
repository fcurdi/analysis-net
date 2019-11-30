using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using Model.Types;
using Assembly = Model.Assembly;
using ECMA335 = System.Reflection.Metadata.Ecma335;
using SRM = System.Reflection.Metadata;

namespace MetadataGenerator.Metadata
{
    internal class MetadataContainer
    {
        public readonly ECMA335.MetadataBuilder metadataBuilder;
        private readonly MetadataResolver metadataResolver;
        public readonly ECMA335.MethodBodyStreamEncoder methodBodyStream;
        private SRM.MethodDefinitionHandle? mainMethodHandle;
        private readonly IDictionary<int, IList<Action>> genericParameters = new Dictionary<int, IList<Action>>();
        private readonly IDictionary<int, Action> nestedTypes = new Dictionary<int, Action>();

        public SRM.MethodDefinitionHandle? MainMethodHandle
        {
            get => mainMethodHandle;
            set
            {
                if (mainMethodHandle != null) throw new Exception("Assembly has more than one main method");
                mainMethodHandle = value;
            }
        }

        public bool Executable => mainMethodHandle != null;

        public MetadataContainer(Assembly assembly)
        {
            metadataBuilder = new ECMA335.MetadataBuilder();
            methodBodyStream = new ECMA335.MethodBodyStreamEncoder(new SRM.BlobBuilder());
            metadataResolver = new MetadataResolver(this, assembly);
        }

        public SRM.StandaloneSignatureHandle ResolveStandaloneSignatureFor(FunctionPointerType method) =>
            metadataResolver.ResolveStandaloneSignatureFor(method);

        public SRM.EntityHandle ResolveReferenceHandleFor(IMetadataReference metadataReference) =>
            metadataResolver.ReferenceHandleOf(metadataReference);

        public void Encode(IType type, ECMA335.SignatureTypeEncoder encoder) => metadataResolver.Encode(type, encoder);

        public void RegisterGenericParameter(SRM.TypeDefinitionHandle owner, GenericParameter genericParameter) =>
            DoRegisterGenericParameter(owner, genericParameter);

        public void RegisterGenericParameter(SRM.MethodDefinitionHandle owner, GenericParameter genericParameter) =>
            DoRegisterGenericParameter(owner, genericParameter);

        // FIXME name
        private void DoRegisterGenericParameter(SRM.EntityHandle owner, GenericParameter genericParameter)
        {
            void GenerateGenericParameter() => metadataBuilder.AddGenericParameter(
                owner,
                GenericParameterAttributes.None, // FIXME ?
                metadataBuilder.GetOrAddString(genericParameter.Name), genericParameter.Index);

            /* FIXME generic constraints not in the model
             if(genericParameter.hasConstraint()){
                 metadataBuilder.AddGenericParameterConstraint(
                 genericParameterHandle, 
                metadataContainer.ResolveReferenceHandleFor(genericParameter.contraint));
             }*/

            var key = ECMA335.CodedIndex.TypeOrMethodDef(owner);
            if (genericParameters.TryGetValue(key, out var actions))
            {
                actions.Add(GenerateGenericParameter);
            }
            else
            {
                genericParameters.Add(key, new List<Action> {GenerateGenericParameter});
            }
        }

        public void GenerateGenericParameters()
        {
            var sortedOwners = genericParameters.Keys.ToImmutableSortedSet();
            foreach (var owner in sortedOwners)
            {
                genericParameters.TryGetValue(owner, out var generateGenericParameterOperations);
                foreach (var generateGenericParameter in generateGenericParameterOperations)
                {
                    generateGenericParameter();
                }
            }
        }

        public void RegisterNestedType(SRM.TypeDefinitionHandle nestedType, SRM.TypeDefinitionHandle enclosingType)
        {
            var key = ECMA335.CodedIndex.TypeOrMethodDef(nestedType);
            nestedTypes.Add(key, () => metadataBuilder.AddNestedType(nestedType, enclosingType));
        }

        public void GenerateNestedTypes()
        {
            var sortedOwners = nestedTypes.Keys.ToImmutableSortedSet();
            foreach (var owner in sortedOwners)
            {
                nestedTypes.TryGetValue(owner, out var addNestedType);
                addNestedType();
            }
        }
    }
}
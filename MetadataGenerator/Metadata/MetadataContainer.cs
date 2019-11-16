using System;
using Model;
using Model.Types;
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
    }
}
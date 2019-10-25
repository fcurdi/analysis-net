using System;
using Model;
using Model.Types;
using ECMA335 = System.Reflection.Metadata.Ecma335;
using SRM = System.Reflection.Metadata;

//FIXME name, package, visibilities, etc
namespace MetadataGenerator
{
    class MetadataContainer
    {
        public readonly ECMA335.MetadataBuilder metadataBuilder;
        private readonly TypeEncoder typeEncoder;
        private readonly ReferenceHandleResolver referenceHandleResolver;
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
            referenceHandleResolver = new ReferenceHandleResolver(metadataBuilder, assembly);
            typeEncoder = new TypeEncoder(referenceHandleResolver);
            methodBodyStream = new ECMA335.MethodBodyStreamEncoder(new SRM.BlobBuilder());
        }

        public SRM.EntityHandle ResolveReferenceHandleFor(IBasicType type) => referenceHandleResolver.ReferenceHandleOf(type);
        public SRM.MemberReferenceHandle ResolveReferenceHandleFor(IMethodReference method, SRM.BlobBuilder signature) => referenceHandleResolver.ReferenceHandleOf(method, signature);
        public SRM.MemberReferenceHandle ResolveReferenceHandleFor(IFieldReference field, SRM.BlobBuilder signature) => referenceHandleResolver.ReferenceHandleOf(field, signature);
        //FIXME name
        public void Encode(IType type, ECMA335.SignatureTypeEncoder encoder) => typeEncoder.Encode(type, encoder);
    }
}

using System.Collections.Generic;
using MetadataGenerator.Generation.Fields;
using MetadataGenerator.Generation.Methods;
using MetadataGenerator.Generation.Methods.Body;
using MetadataGenerator.Generation.Properties;
using MetadataGenerator.Generation.Types;
using Model;
using static MetadataGenerator.Generation.GenericParameterGenerator;
using static MetadataGenerator.Generation.Types.InterfaceImplementationGenerator;
using static MetadataGenerator.Generation.Types.NestedTypeGenerator;
using ECMA335 = System.Reflection.Metadata.Ecma335;
using SRM = System.Reflection.Metadata;

namespace MetadataGenerator.Generation
{
    internal class MetadataContainer
    {
        public readonly ECMA335.MetadataBuilder MetadataBuilder;
        public readonly HandleResolver HandleResolver;
        public readonly FieldSignatureEncoder FieldSignatureEncoder;
        public readonly MethodSignatureEncoder MethodSignatureEncoder;
        public readonly MethodLocalsSignatureEncoder MethodLocalsSignatureEncoder;
        public readonly TypeSignatureEncoder TypeSignatureEncoder;
        public readonly PropertySignatureEncoder PropertySignatureEncoder;
        public readonly ECMA335.MethodBodyStreamEncoder MethodBodyStream;
        public readonly SRM.BlobBuilder MappedFieldData;
        public readonly ISet<GenericParameterEntry> GenericParameterEntries;
        public readonly ISet<InterfaceImplementationEntry> InterfaceImplementationEntries;
        public readonly ISet<NestedTypeEntry> NestedTypeEntries;

        public MetadataContainer(Assembly assembly)
        {
            MetadataBuilder = new ECMA335.MetadataBuilder();
            MethodBodyStream = new ECMA335.MethodBodyStreamEncoder(new SRM.BlobBuilder());
            HandleResolver = new HandleResolver(this, assembly);
            var typeEncoder = new TypeEncoder(HandleResolver);
            FieldSignatureEncoder = new FieldSignatureEncoder(typeEncoder);
            MethodSignatureEncoder = new MethodSignatureEncoder(typeEncoder);
            MethodLocalsSignatureEncoder = new MethodLocalsSignatureEncoder(typeEncoder);
            TypeSignatureEncoder = new TypeSignatureEncoder(typeEncoder);
            PropertySignatureEncoder = new PropertySignatureEncoder(typeEncoder);
            MappedFieldData = new SRM.BlobBuilder();
            GenericParameterEntries = new HashSet<GenericParameterEntry>();
            InterfaceImplementationEntries = new HashSet<InterfaceImplementationEntry>();
            NestedTypeEntries = new HashSet<NestedTypeEntry>();
        }
    }
}
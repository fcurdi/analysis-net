using System.Collections.Generic;
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
            MappedFieldData = new SRM.BlobBuilder();
            GenericParameterEntries = new HashSet<GenericParameterEntry>();
            InterfaceImplementationEntries = new HashSet<InterfaceImplementationEntry>();
            NestedTypeEntries = new HashSet<NestedTypeEntry>();
        }
    }
}
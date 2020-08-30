using System;
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
        public readonly MetadataResolver MetadataResolver;
        public readonly ECMA335.MethodBodyStreamEncoder MethodBodyStream;
        public readonly SRM.BlobBuilder MappedFieldData;
        public readonly ISet<GenericParameterEntry> GenericParameterEntries;
        public readonly ISet<InterfaceImplementationEntry> InterfaceImplementationEntries;
        public readonly ISet<NestedTypeEntry> NestedTypeEntries;
        private SRM.MethodDefinitionHandle? mainMethodHandle;
        private SRM.ModuleDefinitionHandle? moduleHandle;

        public SRM.MethodDefinitionHandle MainMethodHandle
        {
            get => mainMethodHandle ?? throw new Exception("Main method handle was not set");
            set
            {
                if (mainMethodHandle != null) throw new Exception("Main method was already set");
                mainMethodHandle = value;
            }
        }

        public SRM.ModuleDefinitionHandle ModuleHandle
        {
            get => moduleHandle ?? throw new Exception("Module handle was not set");
            set
            {
                if (moduleHandle != null) throw new Exception("Multiple modules not supported");
                moduleHandle = value;
            }
        }

        public MetadataContainer(Assembly assembly)
        {
            MetadataBuilder = new ECMA335.MetadataBuilder();
            MethodBodyStream = new ECMA335.MethodBodyStreamEncoder(new SRM.BlobBuilder());
            MetadataResolver = new MetadataResolver(this, assembly);
            MappedFieldData = new SRM.BlobBuilder();
            GenericParameterEntries = new HashSet<GenericParameterEntry>();
            InterfaceImplementationEntries = new HashSet<InterfaceImplementationEntry>();
            NestedTypeEntries = new HashSet<NestedTypeEntry>();
        }
    }
}
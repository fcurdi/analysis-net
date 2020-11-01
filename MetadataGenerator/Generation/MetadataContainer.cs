using System.Collections.Generic;
using MetadataGenerator.Generation.CustomAttributes;
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
        public readonly ECMA335.MethodBodyStreamEncoder MethodBodyStream;
        public readonly SRM.BlobBuilder MappedFieldData;
        public readonly Encoders Encoders;
        public readonly DelayedEntries DelayedEntries;

        public MetadataContainer(Assembly assembly)
        {
            MetadataBuilder = new ECMA335.MetadataBuilder();
            MethodBodyStream = new ECMA335.MethodBodyStreamEncoder(new SRM.BlobBuilder());
            HandleResolver = new HandleResolver(this, assembly);
            var typeEncoder = new TypeEncoder(HandleResolver);
            Encoders = new Encoders(
                new FieldSignatureEncoder(typeEncoder),
                new MethodSignatureEncoder(typeEncoder),
                new MethodLocalsSignatureEncoder(typeEncoder),
                new TypeSignatureEncoder(typeEncoder),
                new PropertySignatureEncoder(typeEncoder),
                new CustomAttributesSignatureEncoder()
            );
            MappedFieldData = new SRM.BlobBuilder();
            DelayedEntries = new DelayedEntries(
                new HashSet<GenericParameterEntry>(),
                new HashSet<InterfaceImplementationEntry>(),
                new HashSet<NestedTypeEntry>()
            );
        }
    }

    internal class DelayedEntries
    {
        public readonly ISet<GenericParameterEntry> GenericParameterEntries;
        public readonly ISet<InterfaceImplementationEntry> InterfaceImplementationEntries;
        public readonly ISet<NestedTypeEntry> NestedTypeEntries;

        public DelayedEntries(
            ISet<GenericParameterEntry> genericParameterEntries,
            ISet<InterfaceImplementationEntry> interfaceImplementationEntries,
            ISet<NestedTypeEntry> nestedTypeEntries)
        {
            GenericParameterEntries = genericParameterEntries;
            InterfaceImplementationEntries = interfaceImplementationEntries;
            NestedTypeEntries = nestedTypeEntries;
        }
    }

    internal class Encoders
    {
        public readonly FieldSignatureEncoder FieldSignatureEncoder;
        public readonly MethodSignatureEncoder MethodSignatureEncoder;
        public readonly MethodLocalsSignatureEncoder MethodLocalsSignatureEncoder;
        public readonly TypeSignatureEncoder TypeSignatureEncoder;
        public readonly PropertySignatureEncoder PropertySignatureEncoder;
        public readonly CustomAttributesSignatureEncoder CustomAttributesSignatureEncoder;

        public Encoders(
            FieldSignatureEncoder fieldSignatureEncoder,
            MethodSignatureEncoder methodSignatureEncoder,
            MethodLocalsSignatureEncoder methodLocalsSignatureEncoder,
            TypeSignatureEncoder typeSignatureEncoder,
            PropertySignatureEncoder propertySignatureEncoder,
            CustomAttributesSignatureEncoder customAttributesSignatureEncoder)
        {
            FieldSignatureEncoder = fieldSignatureEncoder;
            MethodSignatureEncoder = methodSignatureEncoder;
            MethodLocalsSignatureEncoder = methodLocalsSignatureEncoder;
            TypeSignatureEncoder = typeSignatureEncoder;
            PropertySignatureEncoder = propertySignatureEncoder;
            CustomAttributesSignatureEncoder = customAttributesSignatureEncoder;
        }
    }
}
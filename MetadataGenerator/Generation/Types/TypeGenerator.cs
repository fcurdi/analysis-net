using System;
using System.Collections.Generic;
using System.Linq;
using MetadataGenerator.Generation.CustomAttributes;
using MetadataGenerator.Generation.Fields;
using MetadataGenerator.Generation.Methods;
using MetadataGenerator.Generation.Properties;
using Model.Types;
using static MetadataGenerator.Generation.AttributesProvider;
using static MetadataGenerator.Generation.GenericParameterGenerator;
using static MetadataGenerator.Generation.Types.InterfaceImplementationGenerator;
using ECMA335 = System.Reflection.Metadata.Ecma335;
using SRM = System.Reflection.Metadata;

namespace MetadataGenerator.Generation.Types
{
    internal class TypeGenerator
    {
        private readonly MetadataContainer metadataContainer;
        private readonly CustomAttributeGenerator customAttributeGenerator;
        private readonly MethodGenerator methodGenerator;
        private readonly FieldGenerator fieldGenerator;
        private readonly PropertyGenerator propertyGenerator;

        public TypeGenerator(MetadataContainer metadataContainer)
        {
            this.metadataContainer = metadataContainer;
            customAttributeGenerator = new CustomAttributeGenerator(metadataContainer);
            methodGenerator = new MethodGenerator(metadataContainer, customAttributeGenerator);
            fieldGenerator = new FieldGenerator(metadataContainer, customAttributeGenerator);
            propertyGenerator = new PropertyGenerator(metadataContainer, customAttributeGenerator);
        }

        public SRM.TypeDefinitionHandle Generate(TypeDefinition type)
        {
            var metadataBuilder = metadataContainer.MetadataBuilder;
            var metadataResolver = metadataContainer.HandleResolver;

            var fieldDefinitionHandles = type
                .Fields
                .Select(field => fieldGenerator.Generate(field))
                .ToList();

            var methodToHandle = type
                .Methods
                .ToDictionary(method => method, method => methodGenerator.Generate(method));

            var nextFieldDefinitionHandle =
                ECMA335.MetadataTokens.FieldDefinitionHandle(metadataBuilder.NextRowFor(ECMA335.TableIndex.Field));
            var nextMethodDefinitionHandle =
                ECMA335.MetadataTokens.MethodDefinitionHandle(metadataBuilder.NextRowFor(ECMA335.TableIndex.MethodDef));

            // TypeDef Table (0x02)
            var typeDefinitionHandle = metadataBuilder.AddTypeDefinition(
                attributes: AttributesFor(type),
                @namespace: metadataBuilder.GetOrAddString(type.ContainingNamespace.FullName),
                name: metadataBuilder.GetOrAddString(TypeNameOf(type)),
                baseType: type.Base != null ? metadataResolver.HandleOf(type.Base) : default,
                fieldList: fieldDefinitionHandles.FirstOr(nextFieldDefinitionHandle),
                methodList: methodToHandle.Values.FirstOr(nextMethodDefinitionHandle));

            var propertyDefinitionHandles = type
                .PropertyDefinitions
                .Select(property => propertyGenerator.Generate(property, methodToHandle))
                .ToList();

            if (propertyDefinitionHandles.Count > 0)
            {
                // PropertyMap Table (0x15) 
                metadataBuilder.AddPropertyMap(typeDefinitionHandle, propertyDefinitionHandles.First());
            }

            foreach (var interfaze in type.Interfaces)
            {
                metadataContainer
                    .DelayedEntries
                    .InterfaceImplementationEntries
                    .Add(new InterfaceImplementationEntry(typeDefinitionHandle, metadataResolver.HandleOf(interfaze)));
            }

            foreach (var genericParameter in type.GenericParameters)
            {
                metadataContainer
                    .DelayedEntries
                    .GenericParameterEntries
                    .Add(new GenericParameterEntry(typeDefinitionHandle, genericParameter));
            }

            foreach (var entry in methodToHandle)
            {
                var method = entry.Key;
                var handle = entry.Value;

                foreach (var genericParameter in method.GenericParameters)
                {
                    metadataContainer
                        .DelayedEntries
                        .GenericParameterEntries
                        .Add(new GenericParameterEntry(handle, genericParameter));
                }

                if (method.OverridenMethod != null)
                {
                    metadataBuilder.AddMethodImplementation(
                        typeDefinitionHandle,
                        handle,
                        metadataResolver.HandleOf(method.OverridenMethod)
                    );
                }
            }

            if (type.LayoutInformation.SpecifiesSizes())
            {
                // ClassLayout Table (0x0F) 
                metadataBuilder.AddTypeLayout(
                    typeDefinitionHandle,
                    (ushort) type.LayoutInformation.PackingSize,
                    (uint) type.LayoutInformation.ClassSize);
            }

            foreach (var customAttribute in type.Attributes)
            {
                customAttributeGenerator.Generate(typeDefinitionHandle, customAttribute);
            }

            return typeDefinitionHandle;
        }

        // CLS-compliant generic type names are encoded using the format “name[`arity]”, where […] indicates that the grave accent character
        // “`” and arity together are optional. The encoded name shall follow these rules:
        //     - name shall be an ID that does not contain the “`” character.
        //     - arity is specified as an unsigned decimal number without leading zeros or spaces.
        //     - For a normal generic type, arity is the number of type parameters declared on the type.
        //     - For a nested generic type, arity is the number of newly introduced type parameters.
        public static string TypeNameOf(IBasicType type)
        {
            var typeName = type.Name;
            var isGenericType = type.GenericType == null && type.GenericParameterCount > 0;
            if (isGenericType)
            {
                if (type.ContainingType != null)
                {
                    IList<string> GenericParametersNamesOf(IBasicType iBasicType)
                    {
                        switch (iBasicType)
                        {
                            case BasicType bt: return bt.GenericArguments.Select(elem => ((IBasicType) elem).Name).ToList();
                            case TypeDefinition td: return td.GenericParameters.Select(elem => elem.Name).ToList();
                            default: throw new Exception("Not supported");
                        }
                    }

                    var containingTypeGenericParameters = GenericParametersNamesOf(type.ContainingType);
                    var newlyIntroducedGenericParametersCount =
                        GenericParametersNamesOf(type).Count(parameter => !containingTypeGenericParameters.Contains(parameter));

                    if (newlyIntroducedGenericParametersCount > 0)
                    {
                        typeName = $"{typeName}`{newlyIntroducedGenericParametersCount}";
                    }
                }
                else
                {
                    typeName = $"{typeName}`{type.GenericParameterCount}";
                }
            }

            return typeName;
        }
    }
}
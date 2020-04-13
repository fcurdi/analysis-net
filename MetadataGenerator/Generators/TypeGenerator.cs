using System.Collections.Generic;
using System.Linq;
using MetadataGenerator.Generators.Fields;
using MetadataGenerator.Generators.Methods;
using MetadataGenerator.Generators.Methods.Body;
using MetadataGenerator.Metadata;
using Model.Types;
using static MetadataGenerator.Metadata.AttributesProvider;
using ECMA335 = System.Reflection.Metadata.Ecma335;
using SR = System.Reflection;
using SRM = System.Reflection.Metadata;

namespace MetadataGenerator.Generators
{
    internal class TypeGenerator
    {
        private readonly MetadataContainer metadataContainer;
        private readonly MethodGenerator methodGenerator;
        private readonly FieldGenerator fieldGenerator;
        private readonly PropertyGenerator propertyGenerator;

        public TypeGenerator(MetadataContainer metadataContainer)
        {
            this.metadataContainer = metadataContainer;
            methodGenerator = new MethodGenerator(metadataContainer);
            fieldGenerator = new FieldGenerator(metadataContainer);
            propertyGenerator = new PropertyGenerator(metadataContainer);
        }

        public SRM.TypeDefinitionHandle Generate(TypeDefinition type)
        {
            var methodDefinitionHandles = new List<SRM.MethodDefinitionHandle>();
            var metadataBuilder = metadataContainer.metadataBuilder;
            var fieldDefinitionHandles = type.Fields.Select(field => fieldGenerator.Generate(field)).ToList();
            var methodDefToHandle = new Dictionary<MethodDefinition, SRM.MethodDefinitionHandle>();

            foreach (var method in type.Methods)
            {
                var methodHandle = methodGenerator.Generate(method);
                methodDefinitionHandles.Add(methodHandle);
                if (method.Name.Equals("Main"))
                {
                    metadataContainer.MainMethodHandle = methodHandle;
                }

                methodDefToHandle.Add(method, methodHandle);
            }

            var nextFieldDefinitionHandle = ECMA335.MetadataTokens.FieldDefinitionHandle(metadataBuilder.NextRowFor(ECMA335.TableIndex.Field));
            var nextMethodDefinitionHandle = ECMA335.MetadataTokens.MethodDefinitionHandle(metadataBuilder.NextRowFor(ECMA335.TableIndex.MethodDef));
            var typeDefinitionHandle = metadataBuilder.AddTypeDefinition(
                attributes: GetTypeAttributesFor(type),
                @namespace: metadataBuilder.GetOrAddString(type.ContainingNamespace.FullName),
                name: metadataBuilder.GetOrAddString(TypeNameOf(type)),
                baseType: type.Base != null ? metadataContainer.metadataResolver.HandleOf(type.Base) : default,
                fieldList: fieldDefinitionHandles.FirstOr(nextFieldDefinitionHandle),
                methodList: methodDefinitionHandles.FirstOr(nextMethodDefinitionHandle));

            var firstPropertyDefinitionHandle = type.PropertyDefinitions
                .Select(property => propertyGenerator.Generate(property, methodDefToHandle))
                .FirstOrDefault();

            if (!firstPropertyDefinitionHandle.IsNil)
            {
                metadataContainer.metadataBuilder.AddPropertyMap(typeDefinitionHandle, firstPropertyDefinitionHandle);
            }

            foreach (var interfaze in type.Interfaces)
            {
                metadataContainer.RegisterInterfaceImplementation(typeDefinitionHandle, metadataContainer.metadataResolver.HandleOf(interfaze));
            }

            // generate class generic parameters (Class<T>)
            foreach (var genericParameter in type.GenericParameters)
            {
                metadataContainer.RegisterGenericParameter(typeDefinitionHandle, genericParameter);
            }

            // generate method generic parameters (public T method<T>(T param))
            for (var i = 0; i < type.Methods.Count; i++)
            {
                var method = type.Methods[i];
                foreach (var genericParameter in method.GenericParameters)
                {
                    metadataContainer.RegisterGenericParameter(methodDefinitionHandles[i], genericParameter);
                }
            }

            return typeDefinitionHandle;
        }

        /**
         * CLS-compliant generic type names are encoded using the format “name[`arity]”, where […] indicates that the grave accent character “`” and
         * arity together are optional. The encoded name shall follow these rules:
         *     - name shall be an ID that does not contain the “`” character.
         *     - arity is specified as an unsigned decimal number without leading zeros or spaces.
         *     - For a normal generic type, arity is the number of type parameters declared on the type.
         *     - For a nested generic type, arity is the number of newly introduced type parameters.
         */
        public static string TypeNameOf(IBasicType type)
        {
            var typeName = type.Name;
            if (type.IsGenericType())
            {
                if (type.ContainingType != null)
                {
                    IList<string> GenericParametersNamesOf(IBasicType iBasicType)
                    {
                        switch (iBasicType)
                        {
                            case BasicType bt: return bt.GenericArguments.Select(elem => ((IBasicType) elem).Name).ToList();
                            case TypeDefinition td: return td.GenericParameters.Select(elem => elem.Name).ToList();
                            default: throw new UnhandledCase();
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
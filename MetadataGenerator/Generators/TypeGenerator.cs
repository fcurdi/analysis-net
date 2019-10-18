using System.Collections.Generic;
using Model.Types;
using static MetadataGenerator.AttributesProvider;
using ECMA335 = System.Reflection.Metadata.Ecma335;
using SR = System.Reflection;
using SRM = System.Reflection.Metadata;

namespace MetadataGenerator.Generators
{
    class TypeGenerator
    {
        private readonly MetadataContainer metadataContainer;
        private readonly MethodGenerator methodGenerator;
        private readonly FieldGenerator fieldGenerator;

        public TypeGenerator(MetadataContainer metadataContainer)
        {
            this.metadataContainer = metadataContainer;
            methodGenerator = new MethodGenerator(metadataContainer);
            fieldGenerator = new FieldGenerator(metadataContainer);
        }

        public SRM.TypeDefinitionHandle Generate(TypeDefinition type)
        {
            var fieldDefinitionHandles = new List<SRM.FieldDefinitionHandle>();
            var methodDefinitionHandles = new List<SRM.MethodDefinitionHandle>();
            var metadataBuilder = metadataContainer.metadataBuilder;

            foreach (var field in type.Fields)
            {
                fieldDefinitionHandles.Add(fieldGenerator.Generate(field));
            }

            /* TODO Properties: (works) but model is missing Property concept
             * extrack to PropertiesGenerator

                var propertySignatureBlogBuilder = new BlobBuilder();
                new BlobEncoder(propertySignatureBlogBuilder)
                    .PropertySignature(isInstanceProperty: true) //FIXME when false
                    .Parameters(
                    0,
                    returnType => returnType.Type().Int32(), //FIXME backingField type
                    parameters => { });

                var propertyDefinitionHandle = metadata.AddProperty(
                    attributes: PropertyAttributes.None, //FIXME
                    name: metadata.GetOrAddString(""), //FIXME property name
                    signature: metadata.GetOrAddBlob(propertySignatureBlogBuilder));

                // asociate methods (get, set) to property  
                metadata.AddMethodSemantics(
                    propertyDefinitionHandle,
                    method.Name.StartsWith("get_") ? MethodSemanticsAttributes.Getter : MethodSemanticsAttributes.Setter, //FIXME,
                    methodHandle); //getter/setter
                metadata.AddPropertyMap(typeDefinitionHandle, propertyDefinitionHandle);
            */

            foreach (var method in type.Methods)
            {
                var methodHandle = methodGenerator.Generate(method);
                methodDefinitionHandles.Add(methodHandle);
                if (method.Name.Equals("Main"))
                {
                    metadataContainer.MainMethodHandle = methodHandle;
                }
            }

            var nextFieldDefinitionHandle = ECMA335.MetadataTokens.FieldDefinitionHandle(metadataBuilder.NextRowFor(ECMA335.TableIndex.Field));
            var nextMethodDefinitionHandle = ECMA335.MetadataTokens.MethodDefinitionHandle(metadataBuilder.NextRowFor(ECMA335.TableIndex.MethodDef));
            var typeDefinitionHandle = metadataBuilder.AddTypeDefinition(
                attributes: GetTypeAttributesFor(type),
                @namespace: metadataBuilder.GetOrAddString(type.ContainingNamespace.FullName),
                name: metadataBuilder.GetOrAddString(type.Name),
                baseType: type.Base != null ? metadataContainer.ResolveReferenceHandleFor(type.Base) : default,
                fieldList: fieldDefinitionHandles.FirstOr(nextFieldDefinitionHandle),
                methodList: methodDefinitionHandles.FirstOr(nextMethodDefinitionHandle));

            foreach (var interfaze in type.Interfaces)
            {
                metadataBuilder.AddInterfaceImplementation(type: typeDefinitionHandle, implementedInterface: metadataContainer.ResolveReferenceHandleFor(interfaze));
            }

            /*
               Generic parameters table must be sorted that's why this is done at the end and not during the method generation.
               If done that way, method generic parameters of a type are added before the type's generic parameters and table results unsorted
             */

            // generate class generic parameters (Class<T>)
            foreach (var genericParamter in type.GenericParameters)
            {
                metadataBuilder.AddGenericParameter(
                    typeDefinitionHandle,
                    SR.GenericParameterAttributes.None,
                    metadataBuilder.GetOrAddString(genericParamter.Name),
                    genericParamter.Index);
            }

            // generate method generic parameters (public T method<T>(T param))
            for (int i = 0; i < type.Methods.Count; i++)
            {
                var method = type.Methods[i];
                foreach (var genericParameter in method.GenericParameters)
                {
                    metadataBuilder.AddGenericParameter(
                        methodDefinitionHandles[i],
                        SR.GenericParameterAttributes.None,
                        metadataBuilder.GetOrAddString(genericParameter.Name),
                        genericParameter.Index);
                }
            }

            return typeDefinitionHandle;
        }
    }
}

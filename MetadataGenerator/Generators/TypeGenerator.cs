using System.Collections.Generic;
using System.Linq;
using MetadataGenerator.Generators.Fields;
using MetadataGenerator.Generators.Methods;
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

        public TypeGenerator(MetadataContainer metadataContainer)
        {
            this.metadataContainer = metadataContainer;
            methodGenerator = new MethodGenerator(metadataContainer);
            fieldGenerator = new FieldGenerator(metadataContainer);
        }

        public SRM.TypeDefinitionHandle Generate(TypeDefinition type)
        {
            var methodDefinitionHandles = new List<SRM.MethodDefinitionHandle>();
            var metadataBuilder = metadataContainer.metadataBuilder;
            var fieldDefinitionHandles = type.Fields.Select(field => fieldGenerator.Generate(field)).ToList();

            /* TODO Properties: (works) but model is missing Property concept
             * extract to PropertiesGenerator

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

                // associate methods (get, set) to property  
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
                metadataBuilder.AddInterfaceImplementation(
                    type: typeDefinitionHandle,
                    implementedInterface: metadataContainer.ResolveReferenceHandleFor(interfaze));
            }

            /*
             * Generic parameters table must be sorted that's why this is done at the end and not during the method generation.
             * If done that way, method generic parameters of a type are added before the type's generic parameters and table results unsorted
             */


            // generate class generic parameters (Class<T>)
            foreach (var genericParameter in type.GenericParameters)
            {
                var genericParameterHandle = metadataBuilder.AddGenericParameter(
                    typeDefinitionHandle,
                    SR.GenericParameterAttributes.None, // FIXME
                    metadataBuilder.GetOrAddString(genericParameter.Name),
                    genericParameter.Index);

                /* FIXME generic constraints not in the model
                 if(genericParameter.hasConstraint()){
                     metadataBuilder.AddGenericParameterConstraint(
                         genericParameterHandle, 
                         metadataContainer.ResolveReferenceHandleFor(genericParameter.contraint));
                 }
                 */
            }

            // generate method generic parameters (public T method<T>(T param))
            for (var i = 0; i < type.Methods.Count; i++)
            {
                var method = type.Methods[i];
                foreach (var genericParameter in method.GenericParameters)
                {
                    var genericParameterHandle = metadataBuilder.AddGenericParameter(
                        methodDefinitionHandles[i],
                        SR.GenericParameterAttributes.None, // FIXME
                        metadataBuilder.GetOrAddString(genericParameter.Name),
                        genericParameter.Index);

                    /* FIXME generic constraints not in the model
                      if(genericParameter.hasConstraint()){
                        metadataBuilder.AddGenericParameterConstraint(
                            genericParameterHandle, 
                            metadataContainer.ResolveReferenceHandleFor(genericParameter.contraint));
                       }
                       */
                }
            }

            return typeDefinitionHandle;
        }
    }
}
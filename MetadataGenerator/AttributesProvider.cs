﻿using System;
using System.Reflection;
using Model;
using Model.Types;

namespace MetadataGenerator
{
    public static class AttributesProvider
    {
        public static TypeAttributes GetTypeAttributesFor(TypeDefinition typeDefinition)
        {
            switch (typeDefinition.Kind)
            {
                case TypeDefinitionKind.Class: return ClassTypeAttributes(typeDefinition);
                case TypeDefinitionKind.Enum: return EnumTypeAttributes(typeDefinition);
                case TypeDefinitionKind.Interface: return InterfaceTypeAttributes(typeDefinition);
                case TypeDefinitionKind.Struct: return StructTypeAttributes(typeDefinition);
                case TypeDefinitionKind.Delegate: return DelegateTypeAttributes(typeDefinition);
                default: throw new Exception($"TypeDefinition {typeDefinition.Name} not supported");
            }
        }

        private static TypeAttributes DelegateTypeAttributes(TypeDefinition typeDefinition)
        {
            return TypeAttributes.Class
                | TypeAttributes.Sealed
                | VisibilityAttributesFor(typeDefinition);
        }

        private static TypeAttributes StructTypeAttributes(TypeDefinition typeDefinition)
        {
            return TypeAttributes.Class |
                VisibilityAttributesFor(typeDefinition) |
                TypeAttributes.SequentialLayout |
                TypeAttributes.Sealed |
                TypeAttributes.BeforeFieldInit;  //FIXME: when?
        }

        private static TypeAttributes EnumTypeAttributes(TypeDefinition typeDefinition)
        {
            return TypeAttributes.Class |
                VisibilityAttributesFor(typeDefinition) |
                TypeAttributes.Sealed;
        }

        private static TypeAttributes ClassTypeAttributes(TypeDefinition typeDefinition)
        {
            return TypeAttributes.Class |
                  //TODO: static => abstract & sealed and no BeforeFieldInitBeforeFieldInit
                  TypeAttributes.BeforeFieldInit | //FIXME: when?
                  VisibilityAttributesFor(typeDefinition);
        }

        private static TypeAttributes InterfaceTypeAttributes(TypeDefinition typeDefinition)
        {
            return TypeAttributes.Interface | TypeAttributes.Public | TypeAttributes.Abstract;
        }

        public static FieldAttributes GetFieldAttributesFor(FieldDefinition field)
        {
            var fieldAttributes =
                (field.IsStatic ? FieldAttributes.Static : 0) |
                (field.ContainingType.Kind.Equals(TypeDefinitionKind.Enum) && field.Type.Equals(field.ContainingType) ? FieldAttributes.Literal : 0) | // TODO also applies for const fields. 
                (field.ContainingType.Kind.Equals(TypeDefinitionKind.Enum) && field.Name.Equals("value__", StringComparison.InvariantCultureIgnoreCase) ? FieldAttributes.RTSpecialName | FieldAttributes.SpecialName : 0);
            // (field is readonly ? FieldAttributes.InitOnly : 0); //TODO
            switch (field.Visibility)
            {

                case VisibilityKind.Public:
                    fieldAttributes |= FieldAttributes.Public;
                    break;
                case VisibilityKind.Private:
                    fieldAttributes |= FieldAttributes.Private;
                    break;
                case VisibilityKind.Protected:
                    fieldAttributes |= FieldAttributes.Family;
                    break;
                case VisibilityKind.Internal:
                    fieldAttributes |= FieldAttributes.Assembly;
                    break;
                default:
                    throw field.Visibility.ToUnknownValueException();
            }
            return fieldAttributes;
        }

        public static MethodAttributes GetMethodAttributesFor(MethodDefinition method)
        {
            var isConstructor = method.IsConstructor || method.Name.Equals(".cctor"); // FIXME maybe MethodDefinition can have a isClassConstructor or isTypeInitializer
            var methodAttributes =
                (method.IsAbstract ? MethodAttributes.Abstract : 0) |
                (method.IsStatic ? MethodAttributes.Static : 0) |
                (method.IsVirtual ? MethodAttributes.Virtual : 0) |
                (method.ContainingType.Kind.Equals(TypeDefinitionKind.Interface) ? MethodAttributes.NewSlot : 0) | // FIXME not entirely correct
                (isConstructor ? MethodAttributes.SpecialName | MethodAttributes.RTSpecialName : 0) |
                (method.Name.StartsWith("get_") || method.Name.StartsWith("set_") ? MethodAttributes.SpecialName : 0) | // FIXME fails if non getter/setter method starts with get__/set__
                MethodAttributes.HideBySig; //FIXME when?
            switch (method.Visibility)
            {
                case VisibilityKind.Public:
                    methodAttributes |= MethodAttributes.Public;
                    break;
                case VisibilityKind.Private:
                    methodAttributes |= MethodAttributes.Private;
                    break;
                case VisibilityKind.Protected:
                    methodAttributes |= MethodAttributes.Family;
                    break;
                case VisibilityKind.Internal:
                    methodAttributes |= MethodAttributes.Assembly;
                    break;
                default:
                    throw method.Visibility.ToUnknownValueException();
            }
            return methodAttributes;
        }

        public static ParameterAttributes GetParameterAttributesFor(MethodParameter parameter)
        {
            var attributes = (parameter.HasDefaultValue ? ParameterAttributes.HasDefault : 0);
            switch (parameter.Kind)
            {
                case MethodParameterKind.In:
                    // attributes |= ParameterAttributes.In;
                    //FIXME this seems to be always true... and illspy of original is not
                    break;
                case MethodParameterKind.Out:
                    attributes |= ParameterAttributes.Out;
                    break;
                case MethodParameterKind.Ref:
                    // FIXME
                    break;
            }
            return attributes;
        }

        // FIXME
        private static TypeAttributes VisibilityAttributesFor(TypeDefinition typeDefinition)
        {
            if (typeDefinition.ContainingType != null)
            {
                return (VisibilityKind.Public.Equals(typeDefinition.Visibility) ? TypeAttributes.NestedPublic : TypeAttributes.NestedPrivate);
            }
            else
            {
                return (VisibilityKind.Public.Equals(typeDefinition.Visibility) ? TypeAttributes.Public : TypeAttributes.NotPublic);
            }
        }

    }
}

using System;
using System.Reflection;
using Model;
using Model.Types;

namespace MetadataGenerator
{
    //FIXME class and file name
    public static class AttributesProvider
    {
        public static TypeAttributes GetAttributesFor(Model.Types.TypeDefinition typeDefinition)
        {
            switch (typeDefinition.Kind)
            {
                case Model.Types.TypeDefinitionKind.Class: return ClassTypeAttributes(typeDefinition);
                case Model.Types.TypeDefinitionKind.Enum: return EnumTypeAttributes(typeDefinition);
                case Model.Types.TypeDefinitionKind.Interface: return InterfaceTypeAttributes(typeDefinition);
                case Model.Types.TypeDefinitionKind.Struct: return StructTypeAttributes(typeDefinition);
                case Model.Types.TypeDefinitionKind.Delegate: return DelegateTypeAttributes(typeDefinition);
                default: throw new Exception();
            };
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

        private static TypeAttributes EnumTypeAttributes(Model.Types.TypeDefinition typeDefinition)
        {
            return TypeAttributes.Class |
                VisibilityAttributesFor(typeDefinition) |
                TypeAttributes.Sealed;
        }

        private static TypeAttributes ClassTypeAttributes(Model.Types.TypeDefinition typeDefinition)
        {
            return TypeAttributes.Class |
                  //TODO: static => abstract & sealed and no BeforeFieldInitBeforeFieldInit
                  TypeAttributes.BeforeFieldInit | //FIXME: when?
                  VisibilityAttributesFor(typeDefinition);
        }

        private static TypeAttributes InterfaceTypeAttributes(Model.Types.TypeDefinition typeDefinition)
        {
            return TypeAttributes.Interface | TypeAttributes.Public | TypeAttributes.Abstract;
        }

        public static FieldAttributes GetAttributesFor(Model.Types.FieldDefinition field)
        {
            var fieldAttributes =
                (field.IsStatic ? FieldAttributes.Static : 0) |
                (field.ContainingType.Kind.Equals(Model.Types.TypeDefinitionKind.Enum) ? FieldAttributes.Literal : 0); //FIXME also applies for const fields. 
            // (field is readonly ? FieldAttributes.InitOnly : 0); //FIXME
            switch (field.Visibility)
            {

                case Model.Types.VisibilityKind.Public:
                    fieldAttributes |= FieldAttributes.Public;
                    break;
                case Model.Types.VisibilityKind.Private:
                    fieldAttributes |= FieldAttributes.Private;
                    break;
                case Model.Types.VisibilityKind.Protected:
                    fieldAttributes |= FieldAttributes.Family;
                    break;
                case Model.Types.VisibilityKind.Internal:
                    fieldAttributes |= FieldAttributes.Assembly;
                    break;
                default:
                    throw field.Visibility.ToUnknownValueException();
            }
            return fieldAttributes;
        }

        public static MethodAttributes GetAttributesFor(Model.Types.MethodDefinition method)
        {
            var methodAttributes =
                (method.IsAbstract ? MethodAttributes.Abstract : 0) |
                (method.IsStatic ? MethodAttributes.Static : 0) |
                (method.IsVirtual ? MethodAttributes.Virtual : 0) |
                (method.ContainingType.Kind.Equals(Model.Types.TypeDefinitionKind.Interface) ? MethodAttributes.NewSlot : 0) | // FIXME not entirely correct
                (method.IsConstructor ? MethodAttributes.SpecialName | MethodAttributes.RTSpecialName : 0) | //FIXME should do the same for class constructor (cctor)
                (method.Name.StartsWith("get_") || method.Name.StartsWith("set_") ? MethodAttributes.SpecialName : 0) | //FIXME
                MethodAttributes.HideBySig; //FIXME when?
            switch (method.Visibility)
            {
                case Model.Types.VisibilityKind.Public:
                    methodAttributes |= MethodAttributes.Public;
                    break;
                case Model.Types.VisibilityKind.Private:
                    methodAttributes |= MethodAttributes.Private;
                    break;
                case Model.Types.VisibilityKind.Protected:
                    methodAttributes |= MethodAttributes.Family;
                    break;
                case Model.Types.VisibilityKind.Internal:
                    methodAttributes |= MethodAttributes.Assembly;
                    break;
                default:
                    throw method.Visibility.ToUnknownValueException();
            }
            return methodAttributes;

        }

        //FIXME
        private static TypeAttributes VisibilityAttributesFor(TypeDefinition typeDefinition)
        {
            if (typeDefinition.ContainingType != null)
            {
                return (Model.Types.VisibilityKind.Public.Equals(typeDefinition.Visibility) ? TypeAttributes.NestedPublic : TypeAttributes.NestedPrivate);
            }
            else
            {
                return (Model.Types.VisibilityKind.Public.Equals(typeDefinition.Visibility) ? TypeAttributes.Public : TypeAttributes.NotPublic);
            }
        }

    }
}

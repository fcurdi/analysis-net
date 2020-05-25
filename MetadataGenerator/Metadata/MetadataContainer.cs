using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Model;
using Model.Types;
using ECMA335 = System.Reflection.Metadata.Ecma335;
using SRM = System.Reflection.Metadata;
using static MetadataGenerator.Metadata.AttributesProvider;
using Assembly = Model.Assembly;

namespace MetadataGenerator.Metadata
{
    internal class MetadataContainer
    {
        public readonly ECMA335.MetadataBuilder MetadataBuilder;
        public readonly MetadataResolver MetadataResolver;
        public readonly ECMA335.MethodBodyStreamEncoder MethodBodyStream;
        public readonly SRM.BlobBuilder MappedFieldData;
        private SRM.MethodDefinitionHandle? mainMethodHandle;
        private SRM.ModuleDefinitionHandle? moduleHandle;
        private readonly ISet<GenericParamRow> genericParameterRows = new HashSet<GenericParamRow>();
        private readonly ISet<NestedTypeRow> nestedTypeRows = new HashSet<NestedTypeRow>();
        private readonly ISet<InterfaceImplementationRow> interfaceImplementationRows = new HashSet<InterfaceImplementationRow>();

        public SRM.MethodDefinitionHandle MainMethodHandle
        {
            get => mainMethodHandle ?? throw new Exception("Main method handle was not set");
            set
            {
                if (mainMethodHandle != null) throw new Exception("Assembly has more than one main method");
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
        }

        public void RegisterGenericParameter(SRM.TypeDefinitionHandle parent, GenericParameter genericParameter) =>
            DoRegisterGenericParameter(parent, genericParameter);

        public void RegisterGenericParameter(SRM.MethodDefinitionHandle parent, GenericParameter genericParameter) =>
            DoRegisterGenericParameter(parent, genericParameter);

        private void DoRegisterGenericParameter(SRM.EntityHandle parent, GenericParameter genericParameter) =>
            genericParameterRows.Add(new GenericParamRow(
                parent,
                GetGenericParameterAttributesFor(genericParameter),
                MetadataBuilder.GetOrAddString(genericParameter.Name),
                genericParameter.Index,
                genericParameter.Constraints.Select(type => MetadataResolver.HandleOf(type)).ToSet()
            ));

        public void GenerateGenericParameters() =>
            genericParameterRows
                .OrderBy(row => ECMA335.CodedIndex.TypeOrMethodDef(row.Parent))
                .ThenBy(row => row.Index)
                .ToList()
                .ForEach(row =>
                {
                    var genericParameterHandle = MetadataBuilder.AddGenericParameter(
                        row.Parent,
                        row.Attributes,
                        row.Name,
                        row.Index
                    );
                    foreach (var constraint in row.Constraints)
                    {
                        MetadataBuilder.AddGenericParameterConstraint(genericParameterHandle, constraint);
                    }
                });

        public void RegisterNestedType(SRM.TypeDefinitionHandle type, SRM.TypeDefinitionHandle enclosingType) =>
            nestedTypeRows.Add(new NestedTypeRow(type, enclosingType));

        public void GenerateNestedTypes() =>
            nestedTypeRows
                .OrderBy(row => ECMA335.CodedIndex.TypeDefOrRef(row.Type))
                .ToList()
                .ForEach(row => MetadataBuilder.AddNestedType(row.Type, row.EnclosingType));

        public void RegisterInterfaceImplementation(SRM.TypeDefinitionHandle type, SRM.EntityHandle implementedInterface) =>
            interfaceImplementationRows.Add(new InterfaceImplementationRow(type, implementedInterface));

        public void GenerateInterfaceImplementations() =>
            interfaceImplementationRows
                .OrderBy(row => ECMA335.CodedIndex.TypeDefOrRef(row.Type))
                .ThenBy(row => ECMA335.CodedIndex.TypeDefOrRefOrSpec(row.ImplementedInterface))
                .ToList()
                .ForEach(row => MetadataBuilder.AddInterfaceImplementation(row.Type, row.ImplementedInterface));

        #region Rows

        // FIXME hacen falta los equality members? Deberian mirar todos los campos?

        private class InterfaceImplementationRow
        {
            public readonly SRM.TypeDefinitionHandle Type;
            public readonly SRM.EntityHandle ImplementedInterface;

            public InterfaceImplementationRow(SRM.TypeDefinitionHandle type, SRM.EntityHandle implementedInterface)
            {
                Type = type;
                ImplementedInterface = implementedInterface;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                var other = (InterfaceImplementationRow) obj;
                return Type.Equals(other.Type) && ImplementedInterface.Equals(other.ImplementedInterface);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (Type.GetHashCode() * 397) ^ ImplementedInterface.GetHashCode();
                }
            }

            public static bool operator ==(InterfaceImplementationRow left, InterfaceImplementationRow right)
            {
                return Equals(left, right);
            }

            public static bool operator !=(InterfaceImplementationRow left, InterfaceImplementationRow right)
            {
                return !Equals(left, right);
            }
        }

        private class GenericParamRow
        {
            public readonly SRM.EntityHandle Parent;
            public readonly GenericParameterAttributes Attributes;
            public readonly SRM.StringHandle Name;
            public readonly ushort Index;
            public readonly ISet<SRM.EntityHandle> Constraints;

            public GenericParamRow(
                SRM.EntityHandle parent,
                GenericParameterAttributes attributes,
                SRM.StringHandle name,
                ushort index,
                ISet<SRM.EntityHandle> constraints)
            {
                Parent = parent;
                Attributes = attributes;
                Name = name;
                Index = index;
                Constraints = constraints;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                GenericParamRow other = (GenericParamRow) obj;
                return Parent.Equals(other.Parent) && Attributes == other.Attributes && Name.Equals(other.Name) && Index == other.Index &&
                       Equals(Constraints, other.Constraints);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = Parent.GetHashCode();
                    hashCode = (hashCode * 397) ^ (int) Attributes;
                    hashCode = (hashCode * 397) ^ Name.GetHashCode();
                    hashCode = (hashCode * 397) ^ Index.GetHashCode();
                    hashCode = (hashCode * 397) ^ (Constraints != null ? Constraints.GetHashCode() : 0);
                    return hashCode;
                }
            }

            public static bool operator ==(GenericParamRow left, GenericParamRow right)
            {
                return Equals(left, right);
            }

            public static bool operator !=(GenericParamRow left, GenericParamRow right)
            {
                return !Equals(left, right);
            }
        }

        private class NestedTypeRow
        {
            public readonly SRM.TypeDefinitionHandle Type;
            public readonly SRM.TypeDefinitionHandle EnclosingType;

            public NestedTypeRow(SRM.TypeDefinitionHandle type, SRM.TypeDefinitionHandle enclosingType)
            {
                Type = type;
                EnclosingType = enclosingType;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                var other = (NestedTypeRow) obj;
                return Type.Equals(other.Type) && EnclosingType.Equals(other.EnclosingType);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (Type.GetHashCode() * 397) ^ EnclosingType.GetHashCode();
                }
            }

            public static bool operator ==(NestedTypeRow left, NestedTypeRow right)
            {
                return Equals(left, right);
            }

            public static bool operator !=(NestedTypeRow left, NestedTypeRow right)
            {
                return !Equals(left, right);
            }
        }

        #endregion
    }
}
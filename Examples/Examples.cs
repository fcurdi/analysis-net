using System;

namespace Enums
{

    public enum DefaultEnum
    {
        CONSTANT1,
        CONSTANT2
    }

    public enum EnumOfLongValues : long
    {
        LONG2 = 2L,
        LONG50 = 50L
    }

    public enum EnumOfBytes : byte
    {
        Byte10 = 10,
        Byte20 = 20

    }

    public enum EnumOfUShorts : ushort
    {
        USHORT1 = 1,
        USHORT2 = 2
    }

}

namespace Classes
{

    public class ClassWithProperties
    {
        public double AutoImplementedProperty { get; set; }
        public char AnotherAutoImplementedProperty { get; }

        public int PropertyWithBackingField { get => backingField; set => backingField = value; }
        private int backingField;

        public string AnotherPropertyWithBackingField { get => otherBackingField; }
        private string otherBackingField;

    }

    public class EmptyClass
    {
    }

    public static class StaticClass
    {
        static readonly double StaticDouble;

        static StaticClass()
        {
            StaticDouble = 0.5;
        }
    }


    public class NonEmptyClass
    {
        private readonly int readOnlyIntField = 212;
        public string unassignedString;
        public const string CONST_STRING = "const";

        public void DoNothing() { }

        private int Sum(int arg1, int arg2)
        {

            return arg1 + arg2;
        }

        protected string ReturnStringArg(string arg)
        {
            return arg;
        }

        internal double ReturnADobule(double arg)
        {
            return arg;
        }

        float ReturnAFloat(float arg)
        {
            return arg;
        }

        static void StaticMethod() { }

    }

    public abstract class AbstractClass
    {

        public virtual void VirtualMethod() { }

        public abstract void AbstractMethod();
    }

}

namespace Structs
{
    public struct EmptyStruct
    {
    }

    public struct NonEmptyStruct
    {

        private const string helloMessage = "hello";

        public void DoNothing() { }

        private string Greet()
        {

            return helloMessage;
        }

        private int Sum(int arg1, int arg2)
        {

            return arg1 + arg2;
        }
    }

    //TODO struct with properties

}


namespace Interfaces
{
    public class ClassImplementingInterface : ISampleInterface, IComparable
    {
        public int CompareTo(object obj)
        {
            return 0;
        }

        public void DoSomething()
        {
        }

    }

    public interface ISampleInterface
    {

        void DoSomething();

        // todo  interface can have properties

    }


    public struct ComplexStruct : ISampleInterface, IComparable
    {
        public void DoSomething()
        {
        }

        public int CompareTo(object obj)
        {
            return 0;
        }
    }

}

namespace Hierarchy
{

    public class BaseClass
    {
    }

    public class DerivedClass : BaseClass
    {

    }


    public class ClassDerivedFromSystemClass : Exception
    {

    }

    public class ClassDerivedFromAccessibilityClass : Accessibility.CAccPropServicesClass
    {
    }

}

namespace Nested
{

    public class ClassContainingNestedTypes
    {
        public class NestedClass
        {
        }

        public enum NestedEnum
        {
            CONSTANT1 = 1
        }

        public struct NestedStruct
        {
        }
    }

    public struct StructContainingNestedTypes
    {

        public struct NestedStruct { }

        public enum NestedEnum
        {
            CONSTANT1 = 1
        }

        public class NestedClass
        {
        }
    }

    namespace NestedNamespace
    {

        public class A { }

        namespace NestedNestedNamesace
        {
            public class B
            {
                public class NestedB
                {

                    public class NestedNestedB { }
                }
            }
        }
    }

}

namespace Complex
{

    public class ClassWithMethodsWithNonBuiltInTypes
    {

        private Nested.NestedNamespace.NestedNestedNamesace.B b;

        public Hierarchy.DerivedClass DoSomethingWith(Hierarchy.DerivedClass d)
        {
            return d;
        }

        public Exception DoSomethingWith(Exception e)
        {
            return e;
        }

    }

}


using System;
using System.Collections.Generic;

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

// TODO no yet supported in the framework model
// TODO interface with properties
// TODO struct with properties
namespace PropertiesGettersAndSetters
{

    public class ClassWithProperties
    {
        public double AutoImplementedProperty { get; set; }
        public char AnotherAutoImplementedProperty { get; }

        public int PropertyWithBackingField { get => backingField; set => backingField = value; }
        private int backingField;

        public string AnotherPropertyWithBackingField { get => otherBackingField; }
        private string otherBackingField;

        public void get_ShouldNotHaveSpecialname() { }
    }
}

namespace Classes
{
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


    public class SimpleClass
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

    //FIXME arrays not being generated correctly
    public class ClassWithMoreComplexFieldsAndParamtersOrReturnTypes
    {

        public string[] stringArrayField;
        public Exception[] exceptionArrayField;
        private Nested.NestedNamespace.NestedNestedNamesace.B b;

        public Hierarchy.DerivedClass DoSomethingWith(Hierarchy.DerivedClass d)
        {
            return d;
        }

        public Exception DoSomethingWith(Exception e)
        {
            return e;
        }

        public string[] GetStringArray()
        {
            return new string[] { "hello", "world" };
        }

        public Exception[] GetExceptionArray()
        {
            return new Exception[] { };
        }
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

}

// TODO interface can have properties
namespace Interfaces
{
    public class ClassImplementingInterface : IExtendingSampleInterface, IComparable
    {
        public int CompareTo(object obj)
        {
            return 0;
        }

        public void DoSomething()
        {
        }

        public void DoSomethingExtended()
        {
        }
    }

    public interface IExtendingSampleInterface : ISampleInterface
    {
        void DoSomethingExtended();
    }

    public interface ISampleInterface
    {
        void DoSomething();
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

namespace Delegates
{
    public class SampleClass
    {
        public void DelegateMethod(int x)
        {
        }
    }

    public class ClassThatUsesDelegate
    {

        public delegate void Del(int x);

        //FIXME: code is generating Delegates.Del instead of Delegates.ClassThatUsesDelegate.Del
        public Del ReturnsADelegate() => new Del(new SampleClass().DelegateMethod);

    }
}

namespace Hierarchy
{

    public abstract class AbstractBaseClass
    {
        public abstract void MustImplement();

        public virtual void CanImplement()
        {
        }

    }

    public class BaseClass : AbstractBaseClass
    {
        public override void MustImplement()
        {
            throw new NotImplementedException();
        }

        protected void NotVisibileToDerivedClass()
        {
        }
    }

    public class DerivedClass : BaseClass
    {
        public override void CanImplement() { }

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

// TODO pointers
namespace PointersAndReferences
{

    public class ClassWithMethodWithParametersWithKeywords
    {
        // FIXME ref and out are beign generated like type* instead of type&. Also the bytecodes needs to include the "out" keyword
        public void MethodWithRefAndOutParameters(ref string refString, ref Exception refException, out int outInt, out Classes.SimpleClass outClass)
        {
            outInt = 2;
            outClass = new Classes.SimpleClass();
        }
    }
}

namespace Generics
{
    public class Generic
    {
        public Dictionary<string, Exception> genericField;

        public IList<IList<Exception>> listOfListField;

        public IList<Exception> GetExceptionsList(List<string> _)
        {
            return new List<Exception>();
        }
        /* TODO 
                public T RecievesAndReturnsGeneric<T>(T param)
                {
                    return param;
                }

                public IList<T> RecievesAndReturnsGenericList<T>(IList<T> param)
                {
                    return param;
                }
        */
    }

}


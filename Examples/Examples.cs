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

//TODO implicit, explicit, operator keywords for methods
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

        public SimpleClass(int x, string y)
        {
        }

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

    // TODO var arggs, optional parameters, named arguments, 
    public class ClassWithMoreComplexFieldsAndParamtersOrReturnTypes
    {

        public string[] stringArrayField;
        public string[][] stringArrayArrayField;
        public string[,,] stringJaggedArrayField;
        public Exception[] exceptionArrayField;
        private Nested.NestedNamespace.NestedNestedNamesace.B b;

        public void MethodWithOptionalParameters(
            string someParam,
            int optionalInt = 0,
            Structs.EmptyStruct optionalStruct = new Structs.EmptyStruct())
        { }

        public void MethodWithIntVarArgs(params int[] args)
        {
        }

        public void MethodWithExceptionVarArgs(int someInt, params Exception[] args)
        {
        }

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
        private int number;

        public void DoNothing() { }

        private string Greet()
        {
            return helloMessage;
        }

        private int Sum(int arg1, int arg2)
        {
            return arg1 + arg2;
        }

        public void ModifiesField(int value)
        {
            number = value;
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
        public void SomeMethod(int x)
        {
        }
    }

    public class ClassThatUsesDelegate
    {
        public delegate void Del(int x);

        public Del ReturnsADelegate() => new Del(new SampleClass().SomeMethod);
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
        public NestedClass ReturnsNestedClass() => new NestedClass();
        public NestedClass.NestedNestedClass ReturnsNestedNestedClass() => new NestedClass.NestedNestedClass();
        public System.Reflection.Metadata.BlobBuilder.Blobs ReturnsNestedClassFromOtherAssembly() => new System.Reflection.Metadata.BlobBuilder.Blobs();

        public class NestedClass
        {
            public class NestedNestedClass { }
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

// FIXME ref and out are beign generated like type*& instead of type&. That is because the type a & type in the model is represented as a pointer type.
// FIXME generation not entirely correct
namespace PointersAndReferences
{
    public class PointersAndReferenceClass
    {
        private int number = 1;
        private Exception exception = new Exception();

        public void MethodWithRefAndOutParameters(ref string refString, ref Exception refException, out int outInt, out Classes.SimpleClass outClass)
        {
            outInt = 2;
            outClass = new Classes.SimpleClass(1, "");
        }

        // FIXME not in the model
        public ref int RefInt()
        {
            return ref number;
        }

        // FIXME not in the model
        public ref Exception RefException()
        {
            return ref exception;
        }

        public unsafe void* UnsafeMethod(int* intPointer, Structs.EmptyStruct* structPointer, uint* uintPointer)
        {
            return null;
        }
    }
}

// FIXME generation not entirely correct. 
namespace Generics
{
    public class Generic<C, D>
    {
        public C genericClassTypeField;
        public Dictionary<string, Exception> genericField;
        public IList<IList<Exception>> listOfListField;

        public IList<Exception> GetExceptionsList(List<string> _)
        {
            var a = 1 + 2;
            if (a == 3)
            {

            }
            return new List<Exception>();
        }
        public E RecievesAndReturnsGenericType<T, E, F>(T t, E e)
        {
            return e;
        }

        public IList<T> RecievesAndReturnsGenericTypeList<T>(IList<T> listT)
        {
            return listT;
        }
    }

}

namespace MethodBody
{
    public abstract class ContainingClass
    {
        public void HelloWorld()
        {
            Console.WriteLine("Hello World!");
        }

        public int ReturnsOne()
        {
            return 1;
        }

        public int ReturnsArg(int x)
        {
            return x;
        }

        public int Arithmetics(int x, int y)
        {
            var z = x + y;
            z = x - y;
            z = x * y;
            z = x / y;
            z = x % 2;

            return z;
        }

        public void Logic(bool x, bool y)
        {
            var z = x && y;
            z = x || y;
            z = !x;
            z = x ^ y;
        }

        public void BitwiseOperations(int x)
        {
            var z = x & x;
            z = x | x;
            z = x ^ x;
            z = x >> 1;
            z = z << 1;
        }

        public void Comparison(int x)
        {
            var z = x > 1;
            z = x < 1;
            z = x == 1;
        }

        public void ExceptionHandlingTryCatch(int x)
        {
            try
            {
                var y = 1 / x;
            }
            catch
            {

            }
        }
        public void ExceptionHandlingTryCatchSpecific(int x)
        {
            try
            {
                var y = 1 / x;
            }
            catch (Exception ex)
            {

            }
        }

        public void ExceptionHandlingTryCatchFilter(int x)
        {
            try
            {
                var y = 1 / x;
            }
            catch (Exception ex) when (ex.Message.Contains("by zero"))
            {

            }
        }

        public void ExceptionHandlingTryCatchFinally(Exception e)
        {
            try
            {
                throw e;
            }
            catch
            {
                throw; // rethrow
            }
            finally
            {
                Console.WriteLine("finally");
            }
        }

        public abstract void NoBody();

        public void Alloc()
        {
            unsafe
            {
                var x = stackalloc int[3];
            }
        }

        public void Calls(Classes.SimpleClass simpleClass)
        {
            Console.WriteLine("A method call"); // static
            simpleClass.DoNothing(); // virtual
            Alloc(); // normal
        }

        public void Arrays()
        {
            var intArray = new int[5];
            var ExceptionArray = new Exception[2];
            var stringInitializedArray = new string[] { "hello", "world", "!" };
            unsafe
            {
                var m = new int*[5];
                var k = new int**[2];
            }
        }

        // TODO unboxPtr???
        public void Convert()
        {
            int i = 123;
            object o = i; // boxing

            o = 123;
            i = (int)o;  // unboxing

            // Implicit conversion. A long can hold any value an int can hold, and more!
            int num = 2147483647;
            long bigNum = num;

            double x = 1234.7;
            int a;

            // Cast double to int.
            a = (int)x;
        }

        public void Branch()
        {
            if (!true)
            {
            }
        }

        public void LoadConstant()
        {
            string s = "hello world!"; // ldstr
            int zero = 0; // ldc.i4.0
            int one = 1; // ldc.i4.1
            int two = 2; // ldc.i4.2
            int three = 3; // ldc.i4.3
            int four = 4; // ldc.i4.4
            int five = 5; // ldc.i4.5
            int six = 6; // ldc.i4.6
            int seven = 7; // ldc.i4.7
            int eight = 8; // ldc.i4.8
            int minusOne = -1; // ldc.i4.m1
            int i = 20; // ldc.i4.s 20
            int k = int.MaxValue; // ldc.i4
            long l = long.MinValue;  // ldc.i8
            float f = float.MinValue;  // ldc.r4
            double d = double.MinValue;  // ldc.r8
        }
    }
}

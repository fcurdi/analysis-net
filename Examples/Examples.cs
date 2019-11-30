using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using Accessibility;
using Classes;
using Hierarchy;
using Nested.NestedNamespace.NestedNestedNamesace;
using Structs;

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

// TODO not yet supported in the framework model
// TODO interface with properties
// TODO struct with properties
namespace PropertiesGettersAndSetters
{
    public class ClassWithProperties
    {
        public double AutoImplementedProperty { get; set; }
        public char AnotherAutoImplementedProperty { get; }

        public int PropertyWithBackingField
        {
            get => backingField;
            set => backingField = value;
        }

        private int backingField;

        public string AnotherPropertyWithBackingField
        {
            get => otherBackingField;
        }

        private string otherBackingField;

        public void get_ShouldNotHaveSpecialname()
        {
        }
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
        public static Exception e;
        public static int i;

        static StaticClass()
        {
            StaticDouble = 0.5;
        }

        public static void DoNothing(int x)
        {
        }
    }


    public class SimpleClass
    {
        public readonly int readOnlyIntField = 212;
        public string unassignedString;
        public const string CONST_STRING = "const";

        public SimpleClass(int x, string y)
        {
        }

        public void DoNothing()
        {
        }

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

        static void StaticMethod()
        {
        }
    }

    public abstract class AbstractClass
    {
        public virtual void VirtualMethod()
        {
        }

        public abstract void AbstractMethod();
    }

    // TODO var arggs, optional parameters, named arguments, 
    public class ClassWithMoreComplexFieldsAndParametersOrReturnTypes
    {
        public string[] stringArrayField;
        public string[][] stringArrayArrayField;
        public string[,,] stringJaggedArrayField;
        public Exception[] exceptionArrayField;
        public B b;

        public void MethodWithOptionalParameters(
            string someParam,
            int optionalInt = 0,
            EmptyStruct optionalStruct = new EmptyStruct())
        {
        }

        public void MethodWithIntVarArgs(params int[] args)
        {
        }

        public void MethodWithExceptionVarArgs(int someInt, params Exception[] args)
        {
        }

        public DerivedClass DoSomethingWith(DerivedClass d)
        {
            return d;
        }

        public Exception DoSomethingWith(Exception e)
        {
            return e;
        }

        public string[] GetStringArray()
        {
            return new string[] {"hello", "world"};
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

        public void DoNothing()
        {
        }

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

    public readonly struct StructWithImplicitExplicitKeywords
    {
        private readonly byte digit;

        public StructWithImplicitExplicitKeywords(byte digit)
        {
            this.digit = digit;
        }

        public static implicit operator byte(StructWithImplicitExplicitKeywords d) => d.digit;
        public static explicit operator StructWithImplicitExplicitKeywords(byte b) => new StructWithImplicitExplicitKeywords(b);
    }

    public readonly struct Fraction
    {
        private readonly int num;
        private readonly int den;

        public Fraction(int numerator, int denominator)
        {
            num = numerator;
            den = denominator;
        }

        public static Fraction operator +(Fraction a) => a;
        public static Fraction operator -(Fraction a) => new Fraction(-a.num, a.den);
    }
}

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

// FIXME is generating [Examples.dll]Delegates.ClassThatUsesDelegate/Del ReturnsADelegate  instead of Delegates.ClassThatUsesDelegate/Del ReturnsADelegate 
// (same assembly)
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
            throw new Exception("Not implemented");
        }

        protected void NotVisibileToDerivedClass()
        {
        }
    }

    public class DerivedClass : BaseClass
    {
        public override void CanImplement()
        {
        }
    }

    public class ClassDerivedFromSystemClass : Exception
    {
    }

    public class ClassDerivedFromAccessibilityClass : CAccPropServicesClass
    {
    }
}

namespace Nested
{
    public class ClassContainingNestedTypes
    {
        public NestedClass ReturnsNestedClass() => new NestedClass();
        public NestedClass.NestedNestedClass ReturnsNestedNestedClass() => new NestedClass.NestedNestedClass();

        public BlobBuilder.Blobs ReturnsNestedClassFromOtherAssembly() =>
            new BlobBuilder.Blobs();

        public class NestedClass
        {
            public class NestedNestedClass
            {
            }
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
        public struct NestedStruct
        {
        }

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
        public class A
        {
        }

        namespace NestedNestedNamesace
        {
            public class B
            {
                public class NestedB
                {
                    public class NestedNestedB
                    {
                    }
                }
            }
        }
    }
}

// FIXME ref and out are beign generated like type*& instead of type&. That is because the type a & type in the model is represented as a pointer type.
namespace PointersAndReferences
{
    public class PointersAndReferenceClass
    {
        private int number = 1;
        private Exception exception = new Exception();

        public void MethodWithRefAndOutParameters(ref string refString, ref Exception refException, out int outInt, out SimpleClass outClass)
        {
            outInt = 2;
            outClass = new SimpleClass(1, "");
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

        public unsafe void* UnsafeMethod(int* intPointer, EmptyStruct* structPointer, uint* uintPointer)
        {
            return null;
        }
    }
}

// FIXME generation almost correct. 
namespace Generics
{
    public class Generic<C, D> where D : Exception // FIXME generic constraint not in the model
    {
        public C genericClassTypeField;
        public Dictionary<string, Exception> genericField;

        public IList<IList<Exception>> listOfListField;
        public readonly List<string> stringList = new List<string> {"holas"};

        public IList<Exception> GetExceptionsList(List<string> _)
        {
            var a = 1 + 2;
            if (a == 3)
            {
            }

            return new List<Exception>();
        }

        public void PrintGeneric<T>(T t)
        {
            Console.WriteLine(t.ToString());
        }

        public void MethodWithGenericConstraint<T>(T t) where T : Enum // FIXME generic constraint not in the model
        {
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

    public class GenericClassContainingOtherClasses<T>
    {
        public class NestedClassThatDoesNotAddNewGenericParameters
        {
        }

        public class NestedClassThatAddsNewGenericParameters<U, V>
        {
            public class NestedNestedClassThatAddsNewGenericParameters<W>
            {
            }
        }
    }

    public class ClassThatContainsNestedGenericClass
    {
        public class NestedGenericClass<T>
        {
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


        public abstract void NoBody();

        public void Alloc()
        {
            unsafe
            {
                var x = stackalloc int[3];
            }
        }

        public void Nothing<T>(T arg)
        {
        }

        public void Nothing2<T>()
        {
        }

        // TODO more generic method calls examples
        public void Calls(SimpleClass simpleClass, Action<int> f)
        {
            Console.WriteLine("A method call"); // static
            simpleClass.DoNothing(); // virtual
            Alloc(); // normal

            var l = new List<string> {"holas"};

            f(1); //FIXME callvirt instance void class. The "class" is not being generated. It seems to be only for generic method refs. Same problem that generic not being correctly?

            // TODO calli (indirect)

            /* not working when reading dll
            var g = new Generics.Generic<int, Exception>();
            g.PrintGeneric("hola");
            g.PrintGeneric(1);
            */

            Nothing("");
            Nothing2<int>();
        }

        public void Arrays(EmptyStruct[] structArray)
        {
            byte y = 1;
            short s = 3;
            int x = 2;
            long l = 5;
            float f = 6;
            double d = 7;
            EmptyStruct p;
            var byteArray = new byte[2]; // newarr + stelem.ref
            var shortArray = new short[2]; // newarr + stelem.ref
            var intArray = new int[5]; // newarr + stelem.ref
            var longArray = new long[2]; // newarr + stelem.ref
            var floatArray = new float[2]; // newarr + stelem.ref
            var doubleArray = new double[2]; // newarr + stelem.ref
            var exceptionArray = new Exception[2]; // newarr + stelem.ref
            var stringInitializedArray = new string[] {"hello", "world", "!"}; // newarr + stelem.ref
            unsafe
            {
                var m = new int*[5]; // newarr + stelem.ref
                var k = new int**[2]; // newarr + stelem.ref
            }

            byteArray[1] = y; // stelem.i1
            shortArray[1] = s; // stelem.i2
            intArray[1] = x; // stelem.i4
            longArray[1] = l; // stelem.i8
            floatArray[1] = f; // stelem.r4
            doubleArray[1] = d; // stelem.r8
            structArray[0] = p; // stelem
            // TODO stelem.i
        }

        public void Empty()
        {
        }

        public void Convert(object o)
        {
            long x1 = 1;
            double d = 1.0;
            sbyte x2 = (sbyte) x1; // conv.i1
            short x3 = (short) x1; // conv.i2
            int x4 = (int) x1; // conv.i4
            x1 = (long) d; // conv.i8
            float f = (float) d; // conv.r4
            //TODO conv.r8
            byte x5 = (byte) x1; // conv.u1
            ushort x6 = (ushort) x1; // conv.u2
            uint x7 = (uint) x1; // conv.u4
            var x8 = (ulong) d; // conv.u8
            // TODO conv.i
            // TODO conv.u
            // TODO conv.r.un

            string s = (string) (object) "asd"; // castclass $class
            var x = (int[]) (object) new int[] { };


            // FIXME framework read not working.
            //   var b = o is Classes.SimpleClass; // isinst $class

            object l = 1; // box int
            int i = (int) l; // unbox.any int

            // TODO unbox (unbox ptr)
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
            long c = (char) 9; // ldc.i4.s 9 + conv.i8
            int k = int.MaxValue; // ldc.i4
            long q = int.MaxValue; // ldc.i4 + conv.i8
            long l = long.MinValue; // ldc.i8
            float f = float.MinValue; // ldc.r4
            double d = double.MinValue; // ldc.r8
        }

        public void LoadArgument(int arg1, string arg2, bool arg3, int arg4)
        {
            var i0 = this; // ldarg.0
            var i1 = arg1; // ldarg.1
            var i2 = arg2; // ldarg.2
            var i3 = arg3; // ldarg.3
            var i4 = arg4; // ldarg.s $value
            // TODO ldarg $value (see ecma)
        }

        public void LoadLocal()
        {
            var x0 = 10;
            var x1 = 12;
            var x2 = 23;
            var x3 = 23;
            var x4 = 14;

            var y0 = x0; // ldloc.0
            var y1 = x1; // ldloc.1
            var y2 = x2; // ldloc.2
            var y3 = x3; // ldloc.3
            var y4 = x4; // ldloc.s $index (=4)
            // TODO ldloc $index (see ecma)
        }

        public void LoadAddress(int x)
        {
            unsafe
            {
                var p = default(int*); // ldloca.s 0
                // TODO ldloca $argNum
                var q = &x; // ldarga.s 0
                // TODO ldarga $argNum
            }
        }

        public void LoadPointer()
        {
            Action<int> x = LoadAddress; // ldftn $method
            Action<int> y = null; // ldnull
        }

        public void LoadIndirect(ref object g)
        {
            unsafe
            {
                sbyte a_1 = sbyte.MinValue;
                byte a_2 = byte.MinValue;
                short b_1 = short.MinValue;
                ushort b_2 = ushort.MinValue;
                int c_1 = int.MinValue;
                uint c_2 = uint.MinValue;
                long d = long.MinValue;
                float e = float.MinValue;
                double f = double.MinValue;
                IntPtr h = default;

                var x1 = *&a_1; // ldind.i1 
                var x2 = *&a_2; // ldind.u1 
                var x3 = *&b_1; // ldind.i2 
                var x4 = *&b_2; // ldind.u2 
                var x5 = *&c_1; // ldind.i4 
                var x6 = *&c_2; // ldind.u4 
                var x7 = *&d; // ldind.i8 (ldind.u8 is alias for ldind.i8)
                var x8 = *&e; // ldind.r4 
                var x9 = *&f; // ldind.r8
                var x10 = *&h; // ldind.i
                var x11 = g; // ldind.ref
            }
        }

        public void Compare(int b)
        {
            var a = b == 2; // ceq
            a = b > 2; // cgt
            a = b < 2; // clt
        }

        public void Create()
        {
            new SimpleClass(1, "a"); // newobj $methodCall
            var a = new int[] {1, 2, 3}; // newarr int
            var b = new Exception[] { }; // newarr Clases.SimpleClass
            unsafe
            {
                var c = new int*[] { }; // newarr int*
                var d = new int**[] { }; // newarr int**
            }
        }

        public void LoadArray(Exception[] x, int[] q)
        {
            var a = x[1]; // ldelem.ref
            var b = (new sbyte[] { })[0]; // ldelem.i1
            var c = (new byte[] { })[0]; // ldelem.u1
            var d = (new short[] { })[0]; // ldelem.i2
            var e = (new ushort[] { })[0]; // ldelem.u2
            var f = (new int[] { })[0]; // ldelem.i4
            var g = (new uint[] { })[0]; // ldelem.u4
            var h = (new long[] { })[0]; // ldelem.i8 -- ldelem.u8 (alias)
            var j = (new float[] { })[0]; // ldelem.r4
            var k = (new double[] { })[0]; // ldelem.r8

            // TODO ldelem.i ???
            // TODO ldelem typeTok ???
            // 
            // FIXME framework read not working. Something not implemented? Maybe avoid fixed keyword?
            //            unsafe
            //            {
            //                fixed (int* p = &q[0]) // ldelema $type
            //                {
            //                }
            //            }


            var l = (new int[] {1, 2, 3}).Length; // ldlen
        }

        public void LoadField()
        {
            var a = new SimpleClass(1, "b").unassignedString; // ldfld string $field
            var b = new SimpleClass(1, "b").readOnlyIntField; // ldfld int $field
            var c = new ClassWithMoreComplexFieldsAndParametersOrReturnTypes().b; // ldfld class $field

            //FIXME framework read not working. Something not implemented? Maybe avoid fixed keyword?
            //        unsafe
            //          {
            //                fixed (int* d = &(new Classes.SimpleClass(1, "b")).readOnlyIntField) // ldflda int32 $field
            //                { }
            //            }
        }

        public void StoreField()
        {
            new SimpleClass(1, "b").unassignedString = ""; // stfld string $field
            StaticClass.i = 1; // stsfld int $field
            StaticClass.e = new Exception(); // stsfld Exception $field
        }

        public void StoreValue(int arg)
        {
            int l0, l1, l2, l3, l4;
            l0 = 1; // stloc.0
            l1 = 1; // stloc.1
            l2 = 1; // stloc.2
            l3 = 1; // stloc.3
            l4 = 1; // stloc.s 4
            // TODO stloc indx (not short form)

            arg = 1; // starg.s arg
            // TODO starg arg (not short form)
        }

        public void SizeOf()
        {
            unsafe
            {
                // FIXME framework read not working
                // var x = sizeof(Structs.NonEmptyStruct); // sizeof $type
                var z = sizeof(NonEmptyStruct***); // sizeof $type***
                var y = sizeof(int*); // sizeof int*
            }
        }

        // doesnt work yet
        // TODO other cases
        //        public void LoadToken<T>()
        //{
        //var x = typeof(T);
        //}

        public void Branch(int a, int b, Exception e)
        {
            // TODO
            // beq
            // bge, bge.s
            // bge.un, bge.un.s
            // bgt, bgt.s
            // bgt.un, bgt.un.s
            // ble, ble.s
            // ble.un, ble.un.s
            // blt, blt.s
            // blt.un, blt.un.s
            // bne, bne.s
            // bne.un, bne.un.s
            // br (not short form)
            // brfalse (not short form)
            // brnull.s
            // brnull (not short form)
            // brzero.s
            // brzero (not short form) 
            // brtrue (not short form)
            // brinst.s
            // brinst (not short form)

            goto Label; // br.s 
            Label:
            int x;

            if (a > 2) // brfalse.s
            {
            }

            switch (a)
            {
                case 2: break; // beq.s
            }

            if (e?.Message != null)
            {
            } // brtrue.s
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
    }
}
using System;
using Classes;
using Hierarchy;
using MethodBody;
using Properties;
using Structs;

namespace ExamplesEXE
{
    public static class MainClass
    {
        // TODO pedump --verify all Console/bin/Debug/ExamplesEXE\(generated\).exe 
        // TODO pedump --verify all Console/bin/Debug/Examples\(generated\).dll
        // IMPORTANT ExamplesEXE has Examples as reference so it's important to copy de latest Examples.dll to the Console/bin/debug directory
        public static void Main(string[] args)
        {
            var methodBodyExamples = new MethodBodyExamples();
            var g = 5;
            var sc = new SimpleClass(3, "a");

            Console.WriteLine(methodBodyExamples.Arithmetics(1, 2));
            Console.WriteLine(methodBodyExamples.Compare(6, 90));
            methodBodyExamples.Branch(1, 2, new Exception());
            Console.WriteLine(methodBodyExamples.Convert(new Exception()));
            methodBodyExamples.Empty();
            Console.WriteLine(methodBodyExamples.Logic(true, false));
            methodBodyExamples.Nothing2<String>();
            Console.WriteLine(methodBodyExamples.BitwiseOperations(235));
            methodBodyExamples.HelloWorld();
            Console.WriteLine(methodBodyExamples.LoadArgument(1, "2", true, 4));
            Console.WriteLine(methodBodyExamples.LoadConstant());
            Console.WriteLine(methodBodyExamples.LoadLocal());
            methodBodyExamples.LoadPointer();
            Console.WriteLine(methodBodyExamples.ReturnsArg(10));
            Console.WriteLine(methodBodyExamples.ReturnsOne());
            Console.WriteLine(methodBodyExamples.SizeOf());
            Console.WriteLine(methodBodyExamples.StoreValue(100));
            methodBodyExamples.ExceptionHandlingTryCatchSpecific(0);
            methodBodyExamples.ExceptionHandlingTryCatch(0);
            methodBodyExamples.ExceptionHandlingTryCatchFilter(0);
            Console.WriteLine(methodBodyExamples.LoadField());
            Console.WriteLine(methodBodyExamples.StoreField());
            methodBodyExamples.Calls(sc, e => 5);
            Console.WriteLine(methodBodyExamples.Nothing(new object()));
            Console.WriteLine(methodBodyExamples.Alloc());
            methodBodyExamples.LoadAddress(4);
            Console.WriteLine(methodBodyExamples.LoadToken<Exception>());
            Console.WriteLine(sc.ReceivesArraysAndReturnsIntArray(new[] {""}, new[] {new Exception()}));
            unsafe
            {
                Console.WriteLine(methodBodyExamples.Create());
            }

            Console.WriteLine(methodBodyExamples.Arrays(new[] {new EmptyStruct()}));
            Console.WriteLine(methodBodyExamples.LoadArray(new[] {new Exception("m1"), new Exception("m2")}, new int[5]));
            methodBodyExamples.Calls(sc, e => 5);

            var classWithProperties = new ClassWithProperties();
            classWithProperties.IntPropertyWithBackingField = 2;
            Console.WriteLine(classWithProperties.IntPropertyWithBackingField);
            Console.WriteLine(classWithProperties.StringPropertyWithBackingField);
            classWithProperties.DoublePropertyWithAutoImplementedGetSet = 1.4;
            Console.WriteLine(classWithProperties.DoublePropertyWithAutoImplementedGetSet);
            Console.WriteLine(classWithProperties.BytePropertyWithAutoImplementedGetAndDefaultValue);
            Console.WriteLine(classWithProperties.ExceptionPropertyWithAutoImplementedGetSetAndDefaultValue);
            classWithProperties.ExceptionPropertyWithAutoImplementedGetSetAndDefaultValue = null;
            Console.WriteLine(classWithProperties.ExceptionPropertyWithAutoImplementedGetSetAndDefaultValue);
            classWithProperties.DerivedClassPropertyWithAutoImplementedGetSet = new DerivedClass();
            Console.WriteLine(classWithProperties.DerivedClassPropertyWithAutoImplementedGetSet);

            var structWithProperties = new StructWithProperties();
            structWithProperties.IntPropertyWithBackingField = 2;
            Console.WriteLine(structWithProperties.IntPropertyWithBackingField);
            structWithProperties.DoublePropertyWithAutoImplementedGetSet = 1.4;
            Console.WriteLine(structWithProperties.DoublePropertyWithAutoImplementedGetSet);
            structWithProperties.DerivedClassPropertyWithAutoImplementedGetSet = new DerivedClass();
            Console.WriteLine(structWithProperties.DerivedClassPropertyWithAutoImplementedGetSet);

            /* FIXME Try when that example is fixed
                methodBodyExamples.ExceptionHandlingTryCatchFinally(new AggregateException("Intentionally not catched exception"));
            */
        }
    }

    public class MethodBodyExamples : ContainingClass
    {
        public override void NoBody()
        {
        }
    }
}
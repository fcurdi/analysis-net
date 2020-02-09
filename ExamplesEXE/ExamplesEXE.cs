using System;
using Classes;
using MethodBody;
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
            var g = 5;qq
            var sc = new SimpleClass(3, "a");

            // FIXME some fail due to the ValueType/ReferenceType problem.
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
            Console.WriteLine(methodBodyExamples.LoadIndirect(ref g));
            Console.WriteLine(methodBodyExamples.StoreIndirect(out sbyte b, out short q, out int i, out long l, out float f, out double d,
                out IntPtr ip,
                out SimpleClass s));
            Console.WriteLine(methodBodyExamples.LoadToken<Exception>());
            Console.WriteLine(sc.ReceivesArraysAndReturnsIntArray(new[] {""}, new[] {new Exception()}));
            unsafe
            {
                Console.WriteLine(methodBodyExamples.Create());
            }

            Console.WriteLine(methodBodyExamples.Arrays(new[] {new EmptyStruct()}));
            Console.WriteLine(methodBodyExamples.LoadArray(new[] {new Exception("m1"), new Exception("m2")}, new int[5]));

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
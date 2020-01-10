using MethodBody;

namespace ExamplesEXE
{
    public static class MainClass
    {
        // TODO run peverify /il /md /verbose Console/bin/Debug/ExamplesEXE.exe 
        public static void Main(string[] args)
        {
            // FIXME hay algo con las abstract clases o la herencia. Porque MethodBody.ContainingClass es
            // FIXME abstracta (porque tiene un metodo sin body) y al hacer una clase que herede esa aca y
            // FIXME hacerle un new revienta con lo del vtable
            var a = new ContainingClass();

            /* works [and returns the same as original (in isolation at least)]
                Console.WriteLine(a.Arithmetics(1, 2));
                Console.WriteLine(a.Compare(6, 90));
                a.Branch(1, 2, new Exception());
                Console.WriteLine(a.Convert(new Exception()));
                a.Empty();
                Console.WriteLine(a.Logic(true, false));
                a.Nothing2<String>();
                Console.WriteLine(a.BitwiseOperations(235));
                a.HelloWorld();
                Console.WriteLine(a.LoadArgument(1, "2", true, 4));
                Console.WriteLine(a.LoadConstant());
                Console.WriteLine(a.LoadLocal());
                a.LoadPointer();
                Console.WriteLine(a.ReturnsArg(10));
                Console.WriteLine(a.ReturnsOne());
                Console.WriteLine(a.SizeOf());
                Console.WriteLine(a.StoreValue(100));
                a.ExceptionHandlingTryCatchSpecific(0);
                a.ExceptionHandlingTryCatch(0);
                a.ExceptionHandlingTryCatchFilter(0);
            */

            /* works but differs form original
                a.ExceptionHandlingTryCatchFinally(new AggregateException("Intentionally not catched exception"));
            */

            /* does not work
                a.Alloc();
                Console.WriteLine(a.Arrays(new[] {new EmptyStruct()}));
                a.Calls(new SimpleClass(3, "a"), e => 5);
                Console.WriteLine(a.Nothing(new object()));
                a.LoadAddress(4);
                Console.WriteLine(a.LoadArray(new[] {new Exception("m1"), new Exception("m2")}, new int[5]));
                Console.WriteLine(a.LoadField());
                Console.WriteLine(a.LoadToken<Exception>());
                Console.WriteLine(a.StoreField());
            */

            // pending to try
            // a.Create();
            // a.LoadIndirect();
        }
    }
}
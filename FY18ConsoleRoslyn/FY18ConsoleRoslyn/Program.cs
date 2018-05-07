using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CodeAnalysis;
using System;
using System.Threading.Tasks;
using System.Linq;
using System.Diagnostics;
using System.Threading;

namespace FY18ConsoleRoslyn
{
    public class SampleCalculation
    {
        public double Calc(int p1,int p2,int p3)
        {
            double result = 0;
            Random rnd = new Random();
            for (int j = 0; j < p3; j++)
            {
                int[] numbers = new int[p1];
                for (int i = 1; i < p1; i++)
                    numbers[i] = rnd.Next(i * 10);
                foreach (var item in numbers.Where(p => p % 3 == 0))
                {
                    var p = Math.Sin(item * rnd.NextDouble() * 1.23) + Math.Cos(item * rnd.NextDouble() * 1.23);
                    if (p % 2 == 0) result = result + p; else result = result - p;
                }
                result = result / ( Math.Abs(p3 + p2) + 2.34);
            }
            return result;
        }
    }
    public class Params
    {
        public double gX;
    }
    public class Params3
    {
        public int p1;
        public int p2;
        public int p3;
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            Stopwatch sw;
            var state1 = await CSharpScript.RunAsync<int>("int a = 10;");
            state1 = await state1.ContinueWithAsync<int>("int b=20;");
            state1 = await state1.ContinueWithAsync<int>(
                @"int c=a+b;
                      c / 5
                    ");
            Console.WriteLine(state1.ReturnValue); //6 - please check
            string program2 = @"
                        using System;
                        Math.Sin(gX) * Math.Cos(gX)
                        ";
            var script2 = CSharpScript.Create<double>(program2, ScriptOptions.Default.WithImports("System.Math"), typeof(Params));
            script2.Compile();
            Console.WriteLine($"1: {(await script2.RunAsync(new Params { gX = 1 })).ReturnValue}");
            Console.WriteLine($"2: {(await script2.RunAsync(new Params { gX = 2 })).ReturnValue}");

            //"Test" performance
            string calc3 = @"
                using System;
                using System.Linq;
                public class SampleCalculation
                {
                    public double Calc(int p1,int p2,int p3)
                    {
                        double result = 0;
                        Random rnd = new Random();
                        for (int j = 0; j < p3; j++)
                        {
                            int[] numbers = new int[p1];
                            for (int i = 1; i < p1; i++)
                                numbers[i] = rnd.Next(i * 10);
                            foreach (var item in numbers.Where(p => p % 3 == 0))
                            {
                                var p = Math.Sin(item * rnd.NextDouble() * 1.23) + Math.Cos(item * rnd.NextDouble() * 1.23);
                                if (p % 2 == 0) result = result + p; else result = result - p;
                            }
                            result = result / ( Math.Abs(p3 + p2) + 2.34);
                        }
                        return result;
                    }
                }

                double result = 0;
                for (int i = 0; i < 1000; i++)
                {
                    SampleCalculation c = new SampleCalculation();
                    result += c.Calc(p1, p2, p3);
                }
            ";
            sw = Stopwatch.StartNew();
            var script3 = CSharpScript.Create<double>(calc3,
                ScriptOptions.Default.WithReferences(
                    "System.Core",
                    "System.Linq"
                ),
                
                
                //.WithImports(
                //    "System.Math",
                //    "System.Private.CoreLib",
                //    "System.Collections",
                //    "System.Collections.Concurrent",
                //    "System.Console",
                //    "System.Diagnostics.Debug",
                //    "System.Diagnostics.Process",
                //    "System.Diagnostics.StackTrace",
                //    "System.Globalization",
                //    "System.IO",
                //    "System.Reflection",
                //    "System.Runtime",
                //    "System.Text.Encoding",
                //    "System.Text.RegularExpressions",
                //    "System.Threading",
                //    "System.Threading.Tasks",
                //    "System.Threading.Tasks.Parallel",
                //    "System.Threading.Thread"
                //    ), 
                typeof(Params3));
            var errors = script3.Compile();
            var roslynApi = script3.GetCompilation();
            sw.Stop();
            Console.WriteLine($"Rolsyn compilation: {sw.ElapsedMilliseconds}");
            roslynApi.Emit("filescript3.dll");
            sw = Stopwatch.StartNew();
            await script3.RunAsync(new Params3 { p1=100,p2=200,p3=300 });
            sw.Stop();
            Console.WriteLine($"Full Rolsyn: {sw.ElapsedMilliseconds}");

            sw = Stopwatch.StartNew();
            double result = 0;
            for (int i = 0; i < 1000; i++)
            {
                SampleCalculation c = new SampleCalculation();
                result += c.Calc(100, 200, 300);
            }
            sw.Stop();
            Console.WriteLine($"C#: {sw.ElapsedMilliseconds}");

            string calc4 = @"
                using System;
                using System.Linq;
                public class SampleCalculation
                {
                    public double Calc(int p1,int p2,int p3)
                    {
                        double result = 0;
                        Random rnd = new Random();
                        for (int j = 0; j < p3; j++)
                        {
                            int[] numbers = new int[p1];
                            for (int i = 1; i < p1; i++)
                                numbers[i] = rnd.Next(i * 10);
                            foreach (var item in numbers.Where(p => p % 3 == 0))
                            {
                                var p = Math.Sin(item * rnd.NextDouble() * 1.23) + Math.Cos(item * rnd.NextDouble() * 1.23);
                                if (p % 2 == 0) result = result + p; else result = result - p;
                            }
                            result = result / ( Math.Abs(p3 + p2) + 2.34);
                        }
                        return result;
                    }
                }
                SampleCalculation c = new SampleCalculation();
                c.Calc(p1, p2, p3);
            ";
            var script4 = CSharpScript.Create<double>(calc4,
                ScriptOptions.Default.WithReferences(
                    "System.Core",
                    "System.Linq"
                ),
                typeof(Params3));
            errors = script4.Compile();
            roslynApi = script4.GetCompilation();
            roslynApi.Emit("filescript4.dll");
            var entryPoint = roslynApi.GetEntryPoint(CancellationToken.None);
            result = 0;
            sw = Stopwatch.StartNew();
            for (int i = 0; i < 1000; i++)
            {
                result += script4.RunAsync(new Params3 { p1 = 100, p2 = 200, p3 = 300 }).Result.ReturnValue;
            }

            sw.Stop();
            Console.WriteLine($"Loop + Rolsyn: {sw.ElapsedMilliseconds}");


            Console.WriteLine("END");
            Console.ReadLine();

        }
    }
}

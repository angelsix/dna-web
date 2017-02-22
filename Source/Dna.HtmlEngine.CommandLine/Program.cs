using Dna.HtmlEngine.Core;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Dna.HtmlEngine.CommandLine
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var engine = new DebugEngine
            {
                EngineExtensions = new List<string> { "*.dnaweb" },
                MonitorPath = @"C:\Users\Luke\Desktop\test",
                ProcessDelay = 2000
            })
            {
                // Start the engine
                engine.Start();

                Console.WriteLine("Press enter to stop");
                Console.ReadLine();
            }

            Console.WriteLine("Press any key to close");
            Console.ReadKey();
        }
    }
}
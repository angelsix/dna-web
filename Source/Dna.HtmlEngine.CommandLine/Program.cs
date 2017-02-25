using Dna.HtmlEngine.Core;
using System;
using System.Collections.Generic;
using System.IO;

namespace Dna.HtmlEngine.CommandLine
{
    class Program
    {
        static void Main(string[] args)
        {
            // Create engines
            var engines = new List<BaseEngine> { new DnaHtmlEngine(), new DnaCSharpEngine() };

            // Spin them up
            engines.ForEach(engine => engine.Start());

            // Wait for user to stop it
            Console.WriteLine("Press enter to stop");
            Console.ReadLine();

            // Clean up engines
            engines.ForEach(engine => engine.Dispose());

            Console.WriteLine("Press any key to close");
            Console.ReadKey();
        }
    }
}
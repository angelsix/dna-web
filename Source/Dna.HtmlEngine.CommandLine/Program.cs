using Dna.HtmlEngine.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Dna.HtmlEngine.CommandLine
{
    class Program
    {
        static void Main(string[] args)
        {
            Run(args).Wait();
        }

        static async Task Run(string[] args)
        {
            // Should we just end as soon as we are done?
            var wait = true;

            // Where to monitor for files
            var monitorPath = string.Empty;

            // Configuration file path
            var configPath = string.Empty;

            // Generate all files on start?
            var generateOnStart = GenerateOption.None;

            // Read in arguments
            foreach (var arg in args)
            {
                if (arg == "/n")
                {
                    // Don't wait (just open, process, close)
                    wait = false;
                }
                else if (arg == "/a")
                {
                    // Generate all files on start
                    generateOnStart = GenerateOption.All;
                }
                else if (arg.StartsWith("config="))
                {
                    // Set monitor path
                    configPath = arg.Substring(arg.IndexOf("=") + 1);
                }
                else if (arg.StartsWith("monitor="))
                {
                    // Set monitor path
                    monitorPath = arg.Substring(arg.IndexOf("=") + 1);
                }
            }

            // Create engines
            var engines = new List<BaseEngine> { new DnaHtmlEngine(), new DnaCSharpEngine() };

            // Configure them
            engines.ForEach(engine =>
            {
                // Set monitor path
                engine.MonitorPath = monitorPath;

                // Set configuration file path
                engine.ConfigurationFilePath = configPath;

                // Whether to generate all files on start
                engine.GenerateOnStart = generateOnStart;
            });

            // Spin them up
            foreach (var engine in engines)
                await engine.Start();

            if (wait)
            {
                // Wait for user to stop it
                Console.WriteLine("Press enter to stop");
                Console.ReadLine();
            }

            // Clean up engines
            engines.ForEach(engine => engine.Dispose());

            if (wait)
            {
                Console.WriteLine("Press any key to close");
                Console.ReadKey();
            }
        }
    }
}
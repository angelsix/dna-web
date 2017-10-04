using Dna.Web.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Dna.Web.CommandLine
{
    class Program
    {
        #region Public Properties

        /// <summary>
        /// The Dna Configuration
        /// </summary>
        public static DnaConfiguration Configuration = new DnaConfiguration();

        #endregion

        static void Main(string[] args)
        {
            RunAsync(args).Wait();
        }

        static async Task RunAsync(string[] args)
        {
            // Log useful info
            CoreLogger.Log($"Current Directory: {Environment.CurrentDirectory}");
            CoreLogger.Log("");

            #region Argument Variables

            // Get a specific configuration path override
            var specificConfigurationFile = DnaSettings.SpecificConfigurationFilePath;

            // Read in arguments
            foreach (var arg in args)
            {
                // Override specific configuration file
                if (arg.StartsWith("config="))
                    specificConfigurationFile = arg.Substring(arg.IndexOf("=") + 1);
            }

            #endregion

            #region Read Configuration Files

            // Load configuration files
            Configuration = DnaConfiguration.LoadFromFiles(new[] { DnaSettings.DefaultConfigurationFilePath, specificConfigurationFile });

            #endregion

            #region Configuration Argument Variables

            // Read in arguments
            var overrides = false;
            foreach (var arg in args)
            {
                if (arg == "/c")
                {
                    // Don't wait (just open, process, close)
                    Configuration.ProcessAndClose = true;

                    // Log it
                    CoreLogger.LogTabbed("Argument Override ProcessAndClose", Configuration.ProcessAndClose.ToString(), 1);

                    // Flag so we know to add newline to console log after this
                    overrides = true;
                }
                else if (arg == "/a")
                {
                    // Generate all files on start
                    Configuration.GenerateOnStart = GenerateOption.All;

                    // Log it
                    CoreLogger.LogTabbed("Argument Override GenerateOnStart", Configuration.GenerateOnStart.ToString(), 1);

                    // Flag so we know to add newline to console log after this
                    overrides = true;
                }
                else if (arg.StartsWith("monitor="))
                {
                    // Set monitor path
                    Configuration.MonitorPath = arg.Substring(arg.IndexOf("=") + 1);

                    // Log it
                    CoreLogger.LogTabbed("Argument Override MonitorPath", Configuration.MonitorPath, 1);

                    // Flag so we know to add newline to console log after this
                    overrides = true;
                }
            }

            // Add newline if there are any argument overrides for console log niceness
            if (overrides)
                CoreLogger.Log("");

            #endregion

            // Resolve monitor path
            var unresolvedPath = Configuration.MonitorPath;

            if (!Path.IsPathRooted(unresolvedPath))
                Configuration.MonitorPath = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, unresolvedPath));

            // Log final configuration
            CoreLogger.Log("Final Configuration", type: LogType.Information);
            CoreLogger.Log("-------------------", type: LogType.Information);
            CoreLogger.LogTabbed("Monitor", unresolvedPath, 1, type: LogType.Information);
            CoreLogger.LogTabbed("Monitor Resolved", Configuration.MonitorPath, 1, type: LogType.Information);
            CoreLogger.LogTabbed("Generate On Start", Configuration.GenerateOnStart.ToString(), 1, type: LogType.Information);
            CoreLogger.LogTabbed("Process And Close", Configuration.ProcessAndClose.ToString(), 1, type: LogType.Information);
            CoreLogger.Log("");

            #region Create Engines

            // Create engines
            var engines = new List<BaseEngine> { new DnaHtmlEngine(), new DnaCSharpEngine() };

            // Configure them
            engines.ForEach(engine =>
            {
                // Set monitor path
                engine.MonitorPath = Configuration.MonitorPath;

                // Whether to generate all files on start
                engine.GenerateOnStart = Configuration.GenerateOnStart ?? GenerateOption.None;
            });

            // Spin them up
            foreach (var engine in engines)
                await engine.StartAsync();

            #endregion

            // If we should wait, then wait
            if (Configuration.ProcessAndClose == false)
            {
                // Wait for user to stop it
                Console.WriteLine("Press enter to stop");
                Console.ReadLine();
            }

            // Clean up engines
            engines.ForEach(engine => engine.Dispose());

            // If we should wait, then wait
            if (Configuration.ProcessAndClose == false)
            {
                Console.WriteLine("Press any key to close");
                Console.ReadKey();
            }
        }
    }
}
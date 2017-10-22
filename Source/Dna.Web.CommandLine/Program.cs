using Dna.Web.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
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
            Configuration = DnaConfiguration.LoadFromFiles(new[] { DnaSettings.DefaultConfigurationFilePath, specificConfigurationFile }, null, defaultConfigurationIndex: 0);

            #endregion

            #region Configuration Argument Variables

            // Read in arguments
            var overrides = false;
            foreach (var arg in args)
            {
                // Process and Close
                if (arg == "/c")
                {
                    // Don't wait (just open, process, close)
                    Configuration.ProcessAndClose = true;

                    // Log it
                    CoreLogger.LogTabbed("Argument Override ProcessAndClose", Configuration.ProcessAndClose.ToString(), 1);

                    // Flag so we know to add newline to console log after this
                    overrides = true;
                }
                // Generate All
                else if (arg == "/a")
                {
                    // Generate all files on start
                    Configuration.GenerateOnStart = GenerateOption.All;

                    // Log it
                    CoreLogger.LogTabbed("Argument Override GenerateOnStart", Configuration.GenerateOnStart.ToString(), 1);

                    // Flag so we know to add newline to console log after this
                    overrides = true;
                }
                // Monitor Path
                else if (arg.StartsWith("monitor="))
                {
                    // Set monitor path
                    Configuration.MonitorPath = arg.Substring(arg.IndexOf("=") + 1);

                    // Resolve monitor path
                    var unresolvedPath = Configuration.MonitorPath;

                    if (!Path.IsPathRooted(unresolvedPath))
                        Configuration.MonitorPath = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, unresolvedPath));

                    // Log it
                    CoreLogger.LogTabbed("Argument Override MonitorPath", Configuration.MonitorPath, 1);

                    // Flag so we know to add newline to console log after this
                    overrides = true;
                }
                // Log Level
                else if (arg.StartsWith("logLevel="))
                {
                    // Try get value
                    if (Enum.TryParse<LogLevel>(arg.Substring(arg.IndexOf("=") + 1), out LogLevel result))
                    {
                        // Set new value
                        Configuration.LogLevel = result;

                        // Log it
                        CoreLogger.LogTabbed("Argument Override Log Level", Configuration.LogLevel.ToString(), 1);

                        // Flag so we know to add newline to console log after this
                        overrides = true;
                    }
                }
                // Html Path
                else if (arg.StartsWith("outputPath="))
                {
                    // Set path
                    Configuration.OutputPath = arg.Substring(arg.IndexOf("=") + 1);

                    // Resolve path
                    var unresolvedPath = Configuration.OutputPath;

                    if (!Path.IsPathRooted(unresolvedPath))
                        Configuration.OutputPath = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, unresolvedPath));

                    // Log it
                    CoreLogger.LogTabbed("Argument Override Output Path", Configuration.OutputPath, 1);

                    // Flag so we know to add newline to console log after this
                    overrides = true;
                }
            }

            // Add newline if there are any argument overrides for console log niceness
            if (overrides)
                CoreLogger.Log("");

            #endregion

            #region Load Local Configuration Loop

            // Last loaded monitor path
            string lastMonitorPath;

            // Load the configuration in the monitor path
            do
            {
                lastMonitorPath = Configuration.MonitorPath;

                // Load configuration file from monitor directory
                Configuration = DnaConfiguration.LoadFromFiles(new[] { Path.Combine(Configuration.MonitorPath, DnaSettings.ConfigurationFileName) }, null, Configuration);
            }
            // Looping until it no longer changes
            while (!string.Equals(lastMonitorPath, Configuration.MonitorPath, StringComparison.InvariantCultureIgnoreCase));

            #endregion

            // Log final configuration
            Configuration.LogFinalConfiguration();

            CoreLogger.Log($"DnaWeb Version {typeof(Program).Assembly.GetName().Version}", type: LogType.Attention);
            CoreLogger.Log("", type: LogType.Information);

            #region Create Engines

            // Create engines
            var engines = new List<BaseEngine> { new DnaHtmlEngine(), new DnaCSharpEngine(), new DnaSassEngine() };

            // Configure them
            engines.ForEach(engine =>
            {
                // Set configuration
                engine.Configuration = Configuration;
            });

            // Spin them up
            foreach (var engine in engines)
                engine.Start();

            // Set the core logger log level to match settings now
            CoreLogger.LogLevel = Configuration.LogLevel ?? LogLevel.All;

            // Now do startup generation
            foreach (var engine in engines)
                await engine.StartupGenerationAsync();

            #endregion

            // If we should wait, then wait
            if (Configuration.ProcessAndClose == false)
            {
                // Wait for user commands
                Console.WriteLine("");
                Console.WriteLine("  List of commands  ");
                Console.WriteLine("--------------------");
                Console.WriteLine("   q    Quit");
                Console.WriteLine("");

                var nextCommand = string.Empty;

                do
                {
                    nextCommand = Console.ReadLine();

                }
                while (nextCommand?.Trim().ToUpper() != "Q");
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
using Dna.Web.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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

        /// <summary>
        /// Manager for all LiveServer's
        /// </summary>
        public static LiveServerManager LiveServerManager = new LiveServerManager();

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

            #region All Configuration Files (To Find LiveServers)

            // Search the monitor folder now for all configuration files
            var allConfigurationFiles = BaseEngine.GetDirectoryFiles(Configuration.MonitorPath, DnaSettings.ConfigurationFileName).ToArray();

            // Merge all LiveServer's from the configurations
            Configuration = DnaConfiguration.LoadFromFiles(allConfigurationFiles, null, Configuration, globalSettingsOnly: true);

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

            #region Live Servers

            CoreLogger.Log("", type: LogType.Information);

            // Delay after first open to allow browser to open up 
            // so consecutive opens show in new tabs not new instances
            var firstOpen = true;

            foreach (var directory in Configuration.LiveServerDirectories)
            {
                // Spin up listener
                var listenUrl = LiveServerManager.CreateLiveServer(directory);

                // Open up the listen URL
                if (!string.IsNullOrEmpty(listenUrl))
                {
                    // Open browser
                    OpenBrowser(listenUrl);

                    // Wait if first time
                    if (firstOpen)
                        await Task.Delay(500);

                    // No longer first open
                    firstOpen = false;
                }
            }

            #endregion

            // If we should wait, then wait
            if (Configuration.ProcessAndClose == false)
            {
                // Give time for Live Servers to open browsers and request initial files
                // Allow 500ms per live server as a time to expect the page to have loaded
                await Task.Delay((Configuration.LiveServerDirectories?.Count ?? 0) * 500);

                // Wait for user commands
                Console.WriteLine("");
                Console.WriteLine("  List of commands  ");
                Console.WriteLine("--------------------");
                Console.WriteLine("   q    Quit");
                Console.WriteLine("");

                // Variable for next command
                var nextCommand = string.Empty;

                do
                {
                    // Get next command
                    nextCommand = Console.ReadLine();

                }
                // Until the user types q to quit
                while (nextCommand?.Trim().ToLower() != "q");
            }

            // Clean up engines
            engines.ForEach(engine => engine.Dispose());

            // Stop live servers
            await LiveServerManager.StopAsync();

            // If we should wait, then wait
            if (Configuration.ProcessAndClose == false)
            {
                Console.WriteLine("Press any key to close");
                Console.ReadKey();
            }
        }

        /// <summary>
        /// Opens on URL on this machine
        /// </summary>
        /// <param name="url">The URL to open</param>
        /// <returns></returns>
        private static bool OpenBrowser(string url)
        {
            // For windows use a command line call to start URL
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo("cmd", $"/c start {url.Replace("&", "^&")}") { CreateNoWindow = true });
                return true;
            }
            // For Linux use xdg-open
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", url);
                return true;
            }
            // For Mac use open
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", url);
                return true;
            }

            // Unknown system
            return false;
        }
    }
}
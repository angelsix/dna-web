using Newtonsoft.Json;
using SharpScss;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Dna.Web.Core
{
    /// <summary>
    /// A complete DnaWeb environment that should be created by the application
    /// </summary>
    public class DnaEnvironment
    {
        #region Protected Members

        /// <summary>
        /// A lock for loading the DnaWeb Configuration information
        /// </summary>
        protected object mConfigurationLoadLock = new object();

        /// <summary>
        /// A lock for running any commands after a configuration change
        /// </summary>
        protected object mPostConfigurationChangeLock = new object();

        /// <summary>
        /// An event for canceling out of a Console.ReadLine
        /// </summary>
        protected ManualResetEvent mReadLineResetEvent = new ManualResetEvent(false);

        /// <summary>
        /// True if the user requested an exit of the application
        /// </summary>
        protected bool mUserRequestedCancel;

        #endregion

        #region Public Properties

        /// <summary>
        /// The DnaWeb Configuration
        /// </summary>
        public DnaConfiguration Configuration { get; private set; } = new DnaConfiguration();

        /// <summary>
        /// Manager for all LiveServer's
        /// </summary>
        public LiveServerManager LiveServerManager { get; private set; } = new LiveServerManager();

        /// <summary>
        /// Manager for Live Data Sources
        /// </summary>
        public LiveDataManager LiveDataManager { get; private set; }  = new LiveDataManager();

        /// <summary>
        /// The environment directory DnaWeb is being spun up in... typically Environment.CurrentDirectory
        /// </summary>
        public static string EnvironmentDirectory { get; set; } = Environment.CurrentDirectory;

        /// <summary>
        /// Flag used to temporarily ignore any file changes
        /// </summary>
        public bool DisableWatching { get; set; }

        /// <summary>
        /// All engines running in this environment
        /// </summary>
        public List<BaseEngine> Engines { get; set; } = new List<BaseEngine>();

        /// <summary>
        /// A list of command line arguments used when running this environment
        /// </summary>
        public string[] CommandLineArguments { get; set; }

        /// <summary>
        /// True if the user requested an exit of the application
        /// </summary>
        public bool UserRequestedExit
        {
            get => mUserRequestedCancel;
            set
            {
                // Set value
                mUserRequestedCancel = value;

                // If it was true...
                if (mUserRequestedCancel)
                {
                    // Log it
                    CoreLogger.Log("User requested exit", type: LogType.Warning);

                    // Set the reset event
                    mReadLineResetEvent.Set();
                }
            }
        }

        #endregion

        #region Constructor

        /// <summary>
        /// Default constructor
        /// </summary>
        public DnaEnvironment()
        {

        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Starts the environment and spins up all services, runs until the user exits or the program exits
        /// </summary>
        /// <param name="commandLineArguments">Any command line arguments to pass in</param>
        public async Task RunAsync(string[] commandLineArguments = null)
        {
            CommandLineArguments = commandLineArguments;

            // Log useful info
            CoreLogger.Log($"Current Directory: {EnvironmentDirectory}");
            CoreLogger.Log("");

            #region Load Configuration

            // Load all dna.config files based on 
            LoadConfigurations();

            // Monitor for configuration file changes and reload configurations
            var monitorFolderWatcher = new FolderWatcher
            {
                // Listen out for configuration file changes
                Filter = DnaSettings.ConfigurationFileName,
                Path = Configuration.MonitorPath,
                UpdateDelay = 300
            };

            // Listen for configuration file changes
            monitorFolderWatcher.FileChanged += async (path) =>
            {
                // If we are temporarily ignoring file changes...
                if (DisableWatching)
                    // Return
                    return;

                // Reload configurations
                LoadConfigurations();

                // Run anything that should run each time the configuration changes
                await PostConfigurationMethods();
            };

            // Start watcher
            monitorFolderWatcher.Start();

            #endregion

            #region Create Engines

            // Create engines
            Engines.Add(new DnaHtmlEngine());
            Engines.Add(new DnaCSharpEngine());
            Engines.Add(new DnaSassEngine());
            
            // Configure them
            Engines.ForEach(engine =>
            {
                // Set configuration
                engine.DnaEnvironment = this;
            });

            // Spin them up
            foreach (var engine in Engines)
                engine.Start();

            #endregion

            // Run anything that should run each time the configuration changes
            await PostConfigurationMethods();

            #region Process Commands

            // If we should wait, then wait
            if (Configuration.ProcessAndClose == false)
            {
                // Give time for Live Servers to open browsers and request initial files
                // Allow 500ms per live server as a time to expect the page to have loaded
                await Task.Delay((Configuration.LiveServerDirectories?.Count ?? 0) * 500);

                // Wait for user commands
                OutputCommandList();

                // Variable for next command
                var nextCommand = string.Empty;

                // Loop until quit
                while (!UserRequestedExit)
                {
                    CoreLogger.LogInformation("Command: ", newLine: false, faded: true);

                    // Read next line or 
                    // break if user wanted to exit
                    if (!ReadNextLineOrExit(ref nextCommand) || nextCommand == null)
                    {
                        CoreLogger.Log($"Quiting due to { (UserRequestedExit ? "console quit signal" : "null line") }", type: LogType.Warning);

                        // Either way a null line can only seem to come from a Ctrl+C quit so presume that means to exit
                        // As the Ctrl+C command in Console.CancelKeyPress can come after this read line and after
                        // we get to the check for "Press any key to exit"
                        UserRequestedExit = true;

                        break;
                    }

                    // Ignore blank commands
                    if (string.IsNullOrEmpty(nextCommand))
                        continue;

                    // See if we should quit
                    ProcessCommand(nextCommand, out var quit);

                    // If the command should quit out...
                    if (quit)
                    {
                        CoreLogger.Log("User typed quit command", type: LogType.Diagnostic);
                        break;
                    }
                }
            }

            #endregion

            #region Cleanup

            // Log it
            CoreLogger.Log("Cleaning up...", type: LogType.Warning);

            // Clean up folder watcher
            CoreLogger.Log("Disposing Folder Watchers...", type: LogType.Warning);
            monitorFolderWatcher.Dispose();

            // Clean up engines
            CoreLogger.Log("Disposing Engines...", type: LogType.Warning);
            Engines.ForEach(engine => engine.Dispose());

            // Stop live servers
            CoreLogger.Log("Stopping Live Servers...", type: LogType.Warning);
            await LiveServerManager.StopAsync();

            // If we should wait, then wait
            if (!UserRequestedExit && Configuration.ProcessAndClose == false)
            {
                Console.WriteLine("Press any key to close");
                Console.ReadKey();
            }

            CoreLogger.Log("Exiting", type: LogType.Warning);

            #endregion
        }

        /// <summary>
        /// Loads all configurations from the monitor path and forms a combined final configuration
        /// </summary>
        /// <param name="commandLineArguments">Any command line arguments</param>
        public void LoadConfigurations()
        {
            lock (mConfigurationLoadLock)
            {
                #region Argument Variables

                // Get a specific configuration path override
                var specificConfigurationFile = DnaSettings.SpecificConfigurationFilePath;

                // Read in arguments
                if (CommandLineArguments != null)
                    foreach (var arg in CommandLineArguments)
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

                if (CommandLineArguments != null)
                    foreach (var arg in CommandLineArguments)
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

                            // Resolve any relative paths
                            Configuration.MonitorPath = DnaConfiguration.ResolveFullPath(EnvironmentDirectory, unresolvedPath, false, out var wasRelative);

                            // Log it
                            CoreLogger.LogTabbed("Argument Override MonitorPath", Configuration.MonitorPath, 1);

                            // Flag so we know to add newline to console log after this
                            overrides = true;
                        }
                        // Log Level
                        else if (arg.StartsWith("logLevel="))
                        {
                            // Try get value
                            if (Enum.TryParse<LogLevel>(arg.Substring(arg.IndexOf("=") + 1), out var result))
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
                            // Set path (and resolve if relative)
                            var unresolvedPath = arg.Substring(arg.IndexOf("=") + 1);
                            Configuration.OutputPath = DnaConfiguration.ResolveFullPath(EnvironmentDirectory, unresolvedPath, true, out var wasRelative);

                            // Log it
                            CoreLogger.LogTabbed("Argument Override Output Path", Configuration.OutputPath, 1);

                            // Flag so we know to add newline to console log after this
                            overrides = true;
                        }
                        // Cache Path
                        else if (arg.StartsWith("cachePath="))
                        {
                            // Set Path (and resolve if relative)
                            var unresolvedPath = arg.Substring(arg.IndexOf("=") + 1);
                            Configuration.CachePath = DnaConfiguration.ResolveFullPath(EnvironmentDirectory, unresolvedPath, true, out var wasRelative);

                            // Log it
                            CoreLogger.LogTabbed("Argument Override Cache Path", Configuration.CachePath, 1);

                            // Flag so we know to add newline to console log after this
                            overrides = true;
                        }
                        // Scss Output Style
                        else if (arg.StartsWith("scssOutputStyle="))
                        {
                            // Try and parse it
                            if (Enum.TryParse<ScssOutputStyle>(arg.Substring(arg.IndexOf("=") + 1), out var scssOutputStyle))
                            {
                                // Set new style
                                Configuration.ScssOutputStyle = scssOutputStyle;

                                // Log it
                                CoreLogger.LogTabbed("Argument Override Scss Output Style", Configuration.ScssOutputStyle.ToString(), 1);

                                // Flag so we know to add newline to console log after this
                                overrides = true;
                            }
                        }
                        // Scss Generate Source Map
                        else if (arg.StartsWith("scssGenerateSourceMap="))
                        {
                            // Try and parse it
                            if (bool.TryParse(arg.Substring(arg.IndexOf("=") + 1), out var generateMap))
                            {
                                // Set new value
                                Configuration.ScssGenerateSourceMaps = generateMap;

                                // Log it
                                CoreLogger.LogTabbed("Argument Override Scss Generate Source Map", Configuration.ScssGenerateSourceMaps.ToString(), 1);

                                // Flag so we know to add newline to console log after this
                                overrides = true;
                            }
                        }
                        // Open VS Code
                        else if (arg.StartsWith("openVsCode="))
                        {
                            // Try and parse it
                            if (bool.TryParse(arg.Substring(arg.IndexOf("=") + 1), out var openVsCode))
                            {
                                // Set new value
                                Configuration.OpenVsCode = openVsCode;

                                // Log it
                                CoreLogger.LogTabbed("Argument Override Open VS Code", Configuration.OpenVsCode.ToString(), 1);

                                // Flag so we know to add newline to console log after this
                                overrides = true;
                            }
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
                while (!lastMonitorPath.EqualsIgnoreCase(Configuration.MonitorPath));

                #endregion

                #region Merge Global Settings (from all dna.config files anywhere in project)

                // Search the monitor folder now for all configuration files
                var allConfigurationFiles = FileHelpers.GetDirectoryFiles(Configuration.MonitorPath, DnaSettings.ConfigurationFileName).ToArray();

                // Merge all global settings from the configurations (like Live Servers and Live Data Sources)
                Configuration = DnaConfiguration.LoadFromFiles(allConfigurationFiles, null, Configuration, globalSettingsOnly: true);

                #endregion

                #region Output Final Configuration / App Details

                // Log final configuration
                Configuration.LogFinalConfiguration();

                // Log the version
                LogVersion();
                CoreLogger.Log("", type: LogType.Information);

                #endregion
            }
        }

        /// <summary>
        /// Runs any methods that should run after a configuration change
        /// such as starting Live Servers, downloading Live Data sources etc...
        /// </summary>
        /// <returns></returns>
        public Task PostConfigurationMethods()
        {
            // Lock the call
            return AsyncAwaitor.AwaitAsync(nameof(mPostConfigurationChangeLock), async () =>
            {
                // Set the core logger log level to match settings now
                CoreLogger.LogLevel = Configuration.LogLevel ?? LogLevel.All;

                // Kill any static engines
                var staticEngines = Engines.Where(f => f is StaticEngine).ToList();
                if (staticEngines.Count > 0)
                {
                    // Stop engines
                    staticEngines.ForEach(engine => engine.Dispose());

                    // Remove from list
                    Engines.RemoveAll(engine => engine is StaticEngine);
                }

                // Add any static engines
                Configuration.StaticFolders?.ForEach(staticFolder =>
                {
                    // Create new engine
                    var engine = new StaticEngine
                    {
                        // Set configuration
                        DnaEnvironment = this,
                        StaticFolderDetails = staticFolder,
                        CustomMonitorPath = staticFolder.SourceFolder
                    };

                    // Start the engine
                    engine.Start();

                    // Add it to list
                    Engines.Add(engine);
                });

                // Do startup generation
                await DoStartupGenerationAsync();

                // If we are staying open...
                if (Configuration.ProcessAndClose != true)
                {
                    #region Live Servers

                    // Stop any previous servers
                    await LiveServerManager.StopAsync();

                    // Space for new log output
                    CoreLogger.Log("", type: LogType.Information);

                    // Delay after first open to allow browser to open up 
                    // so consecutive opens show in new tabs not new instances
                    var firstOpen = true;

                    if (Configuration.LiveServerDirectories != null)
                    {
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
                    }

                    #endregion

                    #region Open VS Code
                    
                    // Should we open VS Code?
                    if (Configuration.OpenVsCode == true)
                        OpenVsCode(Configuration.MonitorPath);

                    #endregion

                    // Check for updates
                    AutoUpdateManager.CheckForUpdate();
                }

                #region Live Data

                // Update cache path for Live Data Manager
                LiveDataManager.CachePath = Path.Combine(Configuration.CachePath, DnaSettings.CacheSubFolderLiveData);

                // Refresh local sources
                LiveDataManager.RefreshLocalSources(Configuration.LiveDataSources);

                // Download any out-of-date / unprocessed live sources
                LiveDataManager.DownloadSourcesAsync(Configuration.LiveDataSources);

                #endregion
            });
        }


        /// <summary>
        /// Does the startup generation to generate all files
        /// </summary>
        /// <returns></returns>
        private async Task DoStartupGenerationAsync()
        {
            // Flag all engines as processing so the generation reports come out last
            Engines.ForEach(engine => engine.Processing = true);

            // Now do startup generation
            foreach (var engine in Engines)
                await engine.StartupGenerationAsync();
        }

        /// <summary>
        /// Processes a single line command
        /// </summary>
        /// <param name="command">The command to process</param>
        /// <param name="quit">If true, the environment should quit</param>
        public void ProcessCommand(string command, out bool quit)
        {
            // Make sure it isn't null
            if (string.IsNullOrEmpty(command))
                command = string.Empty;

            // Cleanup command 
            command = command.Trim();

            // Flag if we should quit
            quit = command.EqualsIgnoreCase("q");

            // If we quit, return
            if (quit)
                return;

            // Version
            if (command.EqualsIgnoreCase("version"))
            {
                LogVersion();
            }
            // New configuration file
            if (command.EqualsIgnoreCase("new config"))
            {
                ProcessCommandNewConfiguration(command);
            }
            // Current monitor folder
            else if (command.EqualsIgnoreCase("where"))
            {
                CoreLogger.LogInformation(Configuration.MonitorPath);
            }
            // Download Sources
            else if (command.EqualsIgnoreCase("live update"))
            {
                LiveDataManager.RefreshLocalSources(Configuration.LiveDataSources, log: false);
                LiveDataManager.DownloadSourcesAsync(Configuration.LiveDataSources);
            }
            // Download Sources (force)
            else if (command.EqualsIgnoreCase("live update force"))
            {
                LiveDataManager.RefreshLocalSources(Configuration.LiveDataSources, log: false);
                LiveDataManager.DownloadSourcesAsync(Configuration.LiveDataSources, force: true);
            }
            // List all Live Data Sources
            else if (command.EqualsIgnoreCase("live sources"))
            {
                LiveDataManager.LogAllSources();
            }
            // List Live Data Source details
            else if (command.ToLower().StartsWith("live source "))
            {
                // Extract values split by space
                var sourceArguments = command.Split(' ');

                // We expect name as third argument
                if (sourceArguments.Length != 3)
                {
                    // Log error
                    CoreLogger.Log($"live source has unknown number of commands. Expected 'live source [name]'");
                    return;
                }

                // Log details about this source
                LiveDataManager.LogSourceDetails(sourceArguments[2]);
            }
            // Clean Source Cache
            else if (command.EqualsIgnoreCase("live cache clean"))
            {
                LiveDataManager.DeleteAllSources(Configuration.LiveDataSources);
            }
            // Clean Specific Source Cache
            else if (command.ToLower().StartsWith("live cache clean "))
            {
                // Process delete source command
                ProcessCommandDeleteSource(command);

                // Done
                return;
            }
            // List all Live Data Variables
            else if (command.EqualsIgnoreCase("live variables"))
            {
                LiveDataManager.LogAllVariables();
            }
            // List all Live Data Templates
            else if (command.EqualsIgnoreCase("live templates"))
            {
                LiveDataManager.LogAllTemplates();
            }
            // Show contents of variable
            else if (command.ToLower().StartsWith("live variable "))
            {
                // Show contents
                ProcessCommandShowLiveVariable(command);

                // Done
                return;
            }
            // Live Template
            else if (command.ToLower().StartsWith("new template "))
            {
                // Process new template command
                ProcessCommandNewTemplate(command);
            }
            // Create new Live Data Source
            else if (command.EqualsIgnoreCase("new source"))
            {
                // Process new source command
                ProcessCommandNewSource();
            }
            // Regenerate all content
            else if (command.EqualsIgnoreCase("generate"))
            {
                // Process generate command
                ProcessCommandGenerate();
            }
            else
            {
                // Unknown command
                CoreLogger.LogInformation($"'{command}': Unknown command");

                // Output command list
                OutputCommandList();
            }
        }

        /// <summary>
        /// Outputs the current DnaWeb version to the log
        /// </summary>
        public void LogVersion()
        {
            CoreLogger.Log($"DnaWeb Version {DnaSettings.Version}", type: LogType.Attention);
        }

        /// <summary>
        /// Outputs a list of all available commands to the log
        /// </summary>
        public static void OutputCommandList()
        {
            CoreLogger.LogInformation("");
            CoreLogger.LogInformation("  List of commands      ");
            CoreLogger.LogInformation("------------------------");
            CoreLogger.LogInformation("   version                  DnaWeb version");
            CoreLogger.LogInformation("   where                    Current monitor folder");
            CoreLogger.LogInformation("   live update              Download New Live Data Sources");
            CoreLogger.LogInformation("   live update force        Download All Live Data Sources");
            CoreLogger.LogInformation("   live sources             List all Live Data Sources");
            CoreLogger.LogInformation("   live source [name]       List details of Live Data Source");
            CoreLogger.LogInformation("   live cache clean         Delete all cached Live Data Sources");
            CoreLogger.LogInformation("   live cache clean [name]  Delete specific cached Live Data Source");
            CoreLogger.LogInformation("   live variables           List all available Live Variables");
            CoreLogger.LogInformation("   live variable [name]     Output Live Variable contents to log");
            CoreLogger.LogInformation("   live templates           List all available Live Templates");
            CoreLogger.LogInformation("   new template [name]      Extract specified Live Template");
            CoreLogger.LogInformation("   new source               Create a new blank Live Data Source");
            CoreLogger.LogInformation("   new config               Create a new default dna.config file");
            CoreLogger.LogInformation("   generate                 Regenerates all files for all engines");
            CoreLogger.LogInformation("   q                        Quit");
            CoreLogger.LogInformation("");
        }

        #endregion

        #region Protected Command Methods

        /// <summary>
        /// Processes the show live variable command
        /// </summary>
        /// <param name="command">The command</param>
        protected void ProcessCommandShowLiveVariable(string command)
        {
            // Extract values split by space
            var sourceArguments = command.Split(' ');

            // We expect name as third argument
            if (sourceArguments.Length != 3)
            {
                // Log error
                CoreLogger.Log($"live variable has unknown number of commands. Expected 'live variable [name]'");

                // Stop
                return;
            }

            // Get variable name
            var variableName = sourceArguments[2];

            // Find it
            var foundVariable = LiveDataManager.FindVariable(variableName);

            // If it wasn't found...
            if (foundVariable == null)
            {
                // Log it
                CoreLogger.Log($"Live variable not found '{variableName}'", type: LogType.Warning);

                // Stop
                return;
            }

            // If found, output the variable to the console
            CoreLogger.LogInformation(File.ReadAllText(foundVariable.FilePath), noTime: true);
        }

        /// <summary>
        /// Processes the new template command
        /// </summary>
        /// <param name="command">The command</param>
        protected void ProcessCommandNewTemplate(string command)
        {
            // Extract values split by space
            var templateArguments = command.Split(' ');

            // We expect name as third argument
            if (templateArguments.Length != 3)
            {
                // Log error
                CoreLogger.Log($"new template has unknown number of commands. Expected 'new template [name]'");

                // Stop
                return;
            }

            // Get name
            var name = templateArguments[2];

            // Find template
            var foundTemplate = LiveDataManager.FindTemplate(name);

            // If we didn't find out...
            if (foundTemplate == null)
            {
                // Log it
                CoreLogger.LogInformation($"Template not found '{name}'");

                // Stop
                return;
            }

            // Make sure visual output path ends with \
            var outputPath = Configuration.MonitorPath;
            if (!outputPath.EndsWith("\\"))
                outputPath += '\\';

            // Ask for extraction folder
            CoreLogger.LogInformation($"Extract to: {outputPath}", newLine: false);

            var destination = Console.ReadLine();

            // Resolve path based on the monitor path being the root
            destination = DnaConfiguration.ResolveFullPath(Configuration.MonitorPath, destination, true, out var wasRelative);

            try
            {
                // Disable watching for now while we unzip
                DisableWatching = true;
                
                // Now try extracting this template to the specified folder
                var successful = ZipHelpers.Unzip(foundTemplate.FilePath, destination);

                // If we failed...
                if (!successful)
                    // Log it
                    CoreLogger.LogInformation($"Template not found '{name}'");
                // If we succeeded
                else
                    // Log it
                    CoreLogger.Log($"Template {foundTemplate.Name} successfully extracted to {destination}", type: LogType.Success);
            }
            finally
            {
                // Wait for any pending timeouts
                Task.Delay(Engines.Max(engine => engine.ProcessDelay) + 10).ContinueWith(async (t) =>
                {
                    // Re-enable watching
                    DisableWatching = false;

                    // Reload configurations
                    LoadConfigurations();

                    // Regenerate entire system
                    await PostConfigurationMethods();
                });
            }
        }


        /// <summary>
        /// Processes the new configuration command
        /// </summary>
        /// <param name="command">The command</param>
        protected void ProcessCommandNewConfiguration(string command)
        {
            // Make sure visual output path ends with \
            var outputPath = Configuration.MonitorPath;
            if (!outputPath.EndsWith("\\"))
                outputPath += '\\';

            // Ask for extraction folder
            CoreLogger.LogInformation($"Extract to: {outputPath}", newLine: false);

            var destination = Console.ReadLine();

            // Resolve path based on the monitor path being the root
            destination = DnaConfiguration.ResolveFullPath(Configuration.MonitorPath, destination, true, out var wasRelative);

            // Add configuration file name
            destination = Path.Combine(destination, DnaSettings.ConfigurationFileName);

            // Make sure configuration does not exist
            if (File.Exists(destination))
            {
                // Log it
                CoreLogger.Log($"Configuration file already exists {destination}", type: LogType.Warning);

                // Done
                return;
            }

            try
            {
                // Make sure folder exists
                var destinationFolder = Path.GetDirectoryName(destination);
                if (!Directory.Exists(destinationFolder))
                    Directory.CreateDirectory(destinationFolder);

                // Now save JSON output of a default configuration file
                File.WriteAllText(destination, JsonConvert.SerializeObject(DnaConfiguration.DefaultConfiguration(), Formatting.Indented));

                // Log it
                CoreLogger.Log($"New configuration file created {destination}", type: LogType.Success);
            }
            catch (Exception ex)
            {
                // Log it
                CoreLogger.Log($"Failed to save new configuration file {destination}. {ex.Message}", type: LogType.Error);
            }
        }

        /// <summary>
        /// Processes the new source command
        /// </summary>
        protected void ProcessCommandNewSource()
        {
            // Ask for the details we need

            // Author
            CoreLogger.LogInformation("Author: ", string.Empty, newLine: false, faded: true, noTime: true);
            var author = Console.ReadLine();

            // Name
            CoreLogger.LogInformation("Name: ", string.Empty, newLine: false, faded: true, noTime: true);
            var name = Console.ReadLine();

            // Name
            CoreLogger.LogInformation("Description: ", string.Empty, newLine: false, faded: true, noTime: true);
            var description = Console.ReadLine();

            // Prefix
            CoreLogger.LogInformation("Prefix: ", string.Empty, newLine: false, faded: true, noTime: true);
            var prefix = Console.ReadLine();

            // Make sure visual output path ends with \
            var outputPath = Configuration.MonitorPath;
            if (!outputPath.EndsWith("\\"))
                outputPath += '\\';

            // Destination folder
            var destination = string.Empty;

            // Keep going until folder doesn't exist
            // Ask for extraction folder
            CoreLogger.LogInformation($"Extract to: {outputPath}", newLine: false);
            destination = Console.ReadLine();

            // Resolve path based on the monitor path being the root
            destination = DnaConfiguration.ResolveFullPath(Configuration.MonitorPath, destination, true, out var wasRelative);

            // Get unique name from that location
            destination = FileHelpers.GetUnusedPath(Path.Combine(destination, name));
            
            // Keep track if it is successful
            var successful = false;

            try
            {
                // Stop listening just while we create this source
                DisableWatching = true;

                #region Destination Folder

                // Create the destination folder
                Directory.CreateDirectory(destination);

                #endregion

                #region dna.live.config File

                var liveConfigString = @"{
  ""version"": ""1.0.0"",
  ""author"": ""$$author$$"",
  ""name"": ""$$name$$"",
  ""description"": ""$$description$$"",
  ""prefix"": ""$$prefix$$"",
  ""source"": ""source.zip""
}";

                // Inject values
                liveConfigString = liveConfigString.Replace("$$author$$", author);
                liveConfigString = liveConfigString.Replace("$$name$$", name);
                liveConfigString = liveConfigString.Replace("$$description$$", description);
                liveConfigString = liveConfigString.Replace("$$prefix$$", prefix);

                // Add sample variable inside it
                File.WriteAllText(Path.Combine(destination, DnaSettings.LiveDataConfigurationFileName), liveConfigString);

                #endregion

                #region Readme.md

                // Add sample variable inside it
                File.WriteAllText(Path.Combine(destination, "readme.md"), @"#Custom Live Data Source

## Adding Variables
Add any variables you like to the variables folder. They are simply text files ending with **.dna**

> *NOTE:* The name of the file is the name of the variable.

To find the variable inside DnaWeb the name would be `prefix.name`, so in this example file you would type `" + prefix + @".variable1` to access it.

To use that variable in a file, type `$$!" + prefix + @".variable1$$` then save the file, and watch the file instantly update with the value stored in the variable file (if your editor supports live-updating, like [VS Code](https://code.visualstudio.com/))

## Adding Templates
Templates are just zipped up contents of any kind that you can then extract to a folder using the command `new template [name]`

In this example to extract the template you would type `new template " + prefix + "." + @"blank`

## Installing this Source
There are several ways to install/reference this source. Simply add a reference to any of your **dna.config** files to use it.

You can upload the **dna.live.config** and **source.zip** file to a website, then specify a web link to a **dna.live.config** file that then points to a zip file.

```
    { ""liveDataSources"": [ { ""source"": ""http://www.yoursite.com/SomeFolder/dna.live.config"" } ] }
```

> Remember before uploading to edit your **dna.live.config** (both the one inside and outside of the zip) and change the source value to point to where you upload the zip file, for example http://www.yoursite.com/SomeFolder/source.zip

Alternatively, you can specify a local machine/network link to this **dna.live.config** file

```
    { ""liveDataSources"": [ { ""source"": ""C:\\Users\\luke\\Documents\\DnaWeb\\LocalSource\\dna.live.config"" } ] }
```

Both of the above get their zip files extracted to the Live Data Cache folder and effectively ""installed"". 

For a new version there is no need to change the link in the dna.config file simply have your source **dna.live.config** file update the version number and point to the new zip file then when DnaWeb opens it will detect the new version and install it.

Finally you can have a source that is directly accessed and used, not installed. In order to reference that just extract the source.zip file here (so it contains the Variables, Templates, readme.md and dna.live.config files) and point to this folder:

```
    { ""liveDataSources"": [ { ""source"": ""C:\\Users\\luke\\Documents\\DnaWeb\\LocalSource"" } ] }
```

Now this source will be available whenever DnaWeb starts up.
");

                #endregion

                #region Create Variables

                // Create Variables folder 
                var variableFolder = Path.Combine(destination, DnaSettings.LiveDataFolderVariables);
                Directory.CreateDirectory(variableFolder);

                // Add sample variable inside it
                File.WriteAllText(Path.Combine(variableFolder, $"variable1{DnaSettings.LiveDataFolderVariablesExtension}"), "Replace with your variable data");

                #endregion

                #region Create Templates

                // Create Template folder 
                var templateFolder = Path.Combine(destination, DnaSettings.LiveDataFolderTemplates);
                var templateSourceFolder = Path.Combine(templateFolder, "Source");
                Directory.CreateDirectory(templateSourceFolder);

                // Create dna.config
                File.WriteAllText(Path.Combine(templateSourceFolder, DnaSettings.ConfigurationFileName), @"
{
    ""outputPath"": ""../WebRoot"",
    ""liveServers"": [ ""../WebRoot"" ]
}
");

                // Create Template Html folder 
                var htmlFolder = Path.Combine(templateSourceFolder, "Html");
                Directory.CreateDirectory(htmlFolder);

                // Add a header
                File.WriteAllText(Path.Combine(htmlFolder, $"_header{DnaSettings.DnaWebFileExtension}"), @"<!DOCTYPE html>
<html lang=""en-GB"">
    <head>
        <meta charset=""utf-8"">
        <title>My website</title>
        <link href=""../WebRoot/Assets/Css/style.css"" rel=""stylesheet"" type=""text/css"" />
    </head>
    <body>");

                // Add a footer
                File.WriteAllText(Path.Combine(htmlFolder, $"_footer{DnaSettings.DnaWebFileExtension}"), @"    </body>
</html>");

                // Add index
                File.WriteAllText(Path.Combine(htmlFolder, $"index{DnaSettings.DnaWebFileExtension}"), @"        <h1>Welcome to DnaWeb!</h1>");

                // Create Template Sass folder 
                var sassFolder = Path.Combine(templateSourceFolder, "Sass");
                Directory.CreateDirectory(sassFolder);

                // Create Sass file
                File.WriteAllText(Path.Combine(sassFolder, "style.scss"), @"
html
{
    background: black;
    color: white;
}
");

                // Create Sass dna.config
                File.WriteAllText(Path.Combine(sassFolder, DnaSettings.ConfigurationFileName), @"
{
    ""outputPath"": ""../../WebRoot/Assets/Css""
}
");

                // Now zip template up
                if (!ZipHelpers.Zip(templateFolder, Path.Combine(templateFolder, "blank.zip")))
                {
                    // Log it
                    CoreLogger.Log($"Failed to zip up new source template folder '{templateFolder}'", type: LogType.Error);

                    // Done
                    return;
                }

                // Delete source folder
                Directory.Delete(templateSourceFolder, true);

                #endregion

                #region Zip Up

                if (!ZipHelpers.Zip(destination, Path.Combine(destination, "source.zip")))
                {
                    // Log it
                    CoreLogger.Log($"Failed to zip up new source folder '{destination}'", type: LogType.Error);

                    // Done
                    return;
                }

                #endregion

                #region Clean Up

                // Delete template folder
                Directory.Delete(templateFolder, true);

                // Delete variables folder
                Directory.Delete(variableFolder, true);

                #endregion

                successful = true;

                // Log it
                CoreLogger.Log($"Successfully created new Live Data Source {destination}", type: LogType.Success);

                // Open folder
                OpenFolder(destination);

            }
            catch (Exception ex)
            {
                // Log it
                CoreLogger.Log($"Failed to create new source {destination}. {ex.Message}", type: LogType.Error);
            }
            finally
            {
                // Re-enable file watch
                DisableWatching = false;

                // If it failed...
                if (!successful)
                {
                    // Clean up the destination folder 
                    try
                    {
                        // Delete folder
                        Directory.Delete(destination, true);
                    }
                    catch (Exception ex)
                    {
                        // Log it
                        CoreLogger.Log($"Failed to clean up new source folder {destination}. {ex.Message}", type: LogType.Error);
                    }
                }
            }
        }

        /// <summary>
        /// Processes the clean live source command
        /// </summary>
        /// <param name="command">The command</param>
        protected void ProcessCommandDeleteSource(string command)
        {
            // Extract values split by space
            var sourceArguments = command.Split(' ');

            // We expect name as third argument
            if (sourceArguments.Length != 4)
            {
                // Log error
                CoreLogger.Log($"live cache clean has unknown number of commands. Expected 'live cache clean [name]'");
                return;
            }

            // Get name
            var name = sourceArguments[3];

            // Find source
            var foundSource = LiveDataManager.FindSource(name);

            // If we didn't find a source...
            if (foundSource == null)
            {
                // Log it
                CoreLogger.Log($"Live Data Source '{name}' not found");
                return;
            }

            // Delete source
            LiveDataManager.DeleteSource(foundSource.CachedFilePath, Configuration.LiveDataSources, refreshLocalSources: true);
        }


        /// <summary>
        /// Processes the generate command
        /// </summary>
        protected void ProcessCommandGenerate()
        {
            SafeTask.Run(async () => await DoStartupGenerationAsync());
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Does a <see cref="Console.ReadLine"/> that will return immediately if the
        /// user requests a cancel with <see cref="UserRequestedExit"/>
        /// </summary>
        /// <param name="readLine">The line of data that was read</param>
        /// <returns>Returns true if the line was read, false if the event was canceled</returns>
        private bool ReadNextLineOrExit(ref string readLine)
        {
            // Wait for either a read line or a cancel
            mReadLineResetEvent.Reset();

            // Flag indicating success
            var successful = false;

            // Store result
            var result = string.Empty;

            // Try and read the next line
            SafeTask.Run(() =>
            {
                // Read next line
                result = Console.ReadLine();

                // Flag success 
                successful = true;

                // Return function
                mReadLineResetEvent.Set();
            });

            // Wait for it to finish, or to be canceled
            mReadLineResetEvent.WaitOne();

            // If successful, set result
            if (successful)
                readLine = result;

            // Return if successful
            return successful;
        }

        /// <summary>
        /// Opens a folder on this machine
        /// </summary>
        /// <param name="destination">The folder to open</param>
        private bool OpenFolder(string destination)
        {
            // For windows use explorer
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start("explorer.exe", destination);
                return true;
            }
            // For Linux use xdg-open
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", destination);
                return true;
            }
            // For Mac use open
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", destination);
                return true;
            }

            // Unknown system
            return false;
        }

        /// <summary>
        /// Opens the specified folder in VS Code, if VS Code is installed
        /// </summary>
        /// <returns></returns>
        /// <param name="path">The path to open</param>
        private static bool OpenVsCode(string path)
        {
            // Filename of command line
            var filename = string.Empty;

            // If windows...
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                // It is cmd.exe
                filename = "cmd.exe";
            // If linux...
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                // It is bin/bash
                filename = "/bin/bash";
            // If it is Mac
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                // It is bash
                filename = "bash";
            // Otherwise...
            else
                // Unknown system
                return false;

            // Check VS Code is installed
            var process = Process.Start(new ProcessStartInfo
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
                FileName = filename,
                Arguments = "/C code --version"
            });

            // Start and wait for end
            process.WaitForExit();

            // If it did not exit...
            if (!process.HasExited)
                // Presume it isn't installed
                return false;

            // Get output
            var codeResponse = process.StandardOutput.ReadToEnd();

            // Get the output int lines
            var lines = codeResponse?.Split(new[] { "\n", Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

            // First line should be version string
            // If it isn't...
            if (lines == null || lines.Length <= 0 || !Version.TryParse(lines[0], out var vsVersion))
                // Presume not installed
                return false;

            // If we get here, we have got a valid response with a version number
            // so it is safe to open VS Code with the folder

            // Log it
            CoreLogger.Log($"Opening VS Code for folder '{path}'...", type: LogType.Attention);

            // Open VS Code (on new task otherwise our application doesn't exit unti this does
            var process2 = Process.Start(new ProcessStartInfo
            {
                // IMPORTANT: If you don't specify UseShellExecute = false
                //            and CreateNoWindow = true
                //            then our console will never exit until VS Code is closed
                UseShellExecute = false,
                CreateNoWindow = true,
                FileName = filename,
                Arguments = $@"/C code ""{path}""",
            });

            // Done
            return true;
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

        #endregion
    }
}

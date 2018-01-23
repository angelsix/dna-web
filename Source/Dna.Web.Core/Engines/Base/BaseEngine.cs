using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Dna.Web.Core
{
    /// <summary>
    /// A base engine that any specific engine should implement
    /// </summary>
    public abstract class BaseEngine : IDisposable
    {
        #region Protected Members

        /// <summary>
        /// A Guid to track the last updated file change call
        /// </summary>
        protected Guid mLastUpdateId;

        /// <summary>
        /// A list of files that have changed and need processing on the next loop
        /// </summary>
        protected List<string> mFilesToProcess = new List<string>();

        /// <summary>
        /// A lock for the Files to Process list
        /// </summary>
        protected object mFilesToProcessLock = new object();

        /// <summary>
        /// A list of folder watchers that listen out for file changes of the given extensions
        /// </summary>
        protected List<FolderWatcher> mWatchers;

        /// <summary>
        /// The regex to match special tags containing up to 2 values
        /// For example: <!--@ include header @--> to include the file header._dhtml or header.dhtml if found
        /// </summary>
        protected string mStandard2GroupRegex = @"<!--@\s*(\w+)\s*(.*?)\s*@-->";

        /// <summary>
        /// The regex to match special tags containing variables and data (which are stored as XML inside the tag)
        /// </summary>
        protected string mStandardVariableRegex = @"<!--\$(.+?(?=\$-->))\$-->";

        /// <summary>
        /// The regex used to find Live Variables to be replaced with the values
        /// $$!variable$$
        /// </summary>
        protected string mLiveVariableUseRegex = @"\$\$!(.+?(?=\$\$))\$\$";

        /// <summary>
        /// The regex used to find variables to be replaced with the values
        /// $$variable$$
        /// </summary>
        protected string mVariableUseRegex = @"\$\$(?!!)(.+?(?=\$\$))\$\$";

        /// <summary>
        /// The prefixed string in front of a variable to flag it as a special Dna variable
        /// </summary>
        protected string mDnaVariablePrefix = "dna.";

        /// <summary>
        /// The regex used to find a Dna Variable with it's contents wrapped inside Date("contents")
        /// </summary>
        protected string mDnaVariableDateRegex = @"Date\(""(.+?(?=""\)))""\)";

        /// <summary>
        /// The name of the Dna Variable for getting the executing current directory (project path)
        /// </summary>
        protected string mDnaVariableProjectPath = "ProjectPath";

        /// <summary>
        /// The name of the Dna Variable for getting the full file path of the file this variable resides inside
        /// </summary>
        protected string mDnaVariableFilePath = "FilePath";

        #endregion

        #region Protected Properties

        /// <summary>
        /// Flag indicating if the <see cref="ProcessMainTags(FileProcessingData)"/> function will run when 
        /// processing files in this engine
        /// </summary>
        public bool WillProcessMainTags { get; set; } = true;

        /// <summary>
        /// Flag indicating if the <see cref="ProcessDataTags(FileProcessingData)"/> function will run when 
        /// processing files in this engine
        /// </summary>
        public bool WillProcessDataTags { get; set; } = true;

        /// <summary>
        /// Flag indicating if the <see cref="ProcessOutputTags(FileProcessingData)"/> function will run when 
        /// processing files in this engine
        /// </summary>
        public bool WillProcessOutputTags { get; set; } = true;

        /// <summary>
        /// Flag indicating if the <see cref="GenerateOutput(FileProcessingData, FileOutputData)"/> function will
        /// extract variables from the files when processing files in this engine
        /// </summary>
        public bool WillProcessVariables { get; set; } = true;

        /// <summary>
        /// Flag indicating if the <see cref="GenerateOutput(FileProcessingData, FileOutputData)"/> function will
        /// process live variables from the files when processing files in this engine
        /// </summary>
        public bool WillProcessLiveVariables { get; set; } = true;

        /// <summary>
        /// Flag indicating if the <see cref="GenerateOutput(FileProcessingData, FileOutputData)"/> function will
        /// read the files contents into memory as <see cref="FileProcessingData.UnprocessedFileContents"/> 
        /// from the files when processing files in this engine
        /// </summary>
        public bool WillReadFileIntoMemory { get; set; } = true;

        /// <summary>
        /// A cached list of all monitored files since the last file change
        /// </summary>
        public List<(string Path, List<string> References)> AllMonitoredFiles { get; set; } = new List<(string Path, List<string> References)>();

        /// <summary>
        /// If true, causes a `generate` command to run if any folder inside the watched folder gets renamed
        /// to ensure the entire structure is still valid
        /// </summary>
        public bool RegenerateOnFolderRename { get; set; } = true;

        /// <summary>
        /// If true, causes a file change event when a file is renamed
        /// </summary>
        public bool TreatFileRenameAsChange { get; set; } = true;

        #endregion

        #region Public Properties

        /// <summary>
        /// The human-readable name of this engine
        /// </summary>
        public abstract string EngineName { get; }

        /// <summary>
        /// A flag indicating if this engine is busy processing something
        /// </summary>
        public bool Processing { get; set; }

        /// <summary>
        /// The DnaWeb Environment this engine is running inside of
        /// </summary>
        public DnaEnvironment DnaEnvironment { get; set; }

        /// <summary>
        /// The desired default output extension for generated files if not overridden
        /// </summary>
        public string OutputExtension { get; set; } = ".dna";

        /// <summary>
        /// The time in milliseconds to wait for file edits to stop occurring before processing the file
        /// </summary>
        public int ProcessDelay { get; set; } = 300;

        /// <summary>
        /// The filename extensions to monitor for
        /// All files: .*
        /// Specific types: .dhtml
        /// </summary>
        public List<string> EngineExtensions { get; set; }

        /// <summary>
        /// The unique key to lock file change processes so that only one process loop happens at once
        /// </summary>
        public string FileChangeLockKey => "FileChangeLock";

        /// <summary>
        /// A monitor path to use instead of the <see cref="DnaEnvironment"/> monitor path
        /// </summary>
        public string CustomMonitorPath { get; set; }

        /// <summary>
        /// The monitor path to use for this engine
        /// </summary>
        public string ResolvedMonitorPath => CustomMonitorPath ?? DnaEnvironment?.Configuration.MonitorPath;

        /// <summary>
        /// The results of the last generation run
        /// for the generated files
        /// </summary>
        public List<string> LastGenerationGeneratedFiles { get; private set; }

        /// <summary>
        /// The results of the last generation run
        /// for the processed files
        /// </summary>
        public List<string> LastGenerationProcessedFiles { get; private set; }

        /// <summary>
        /// The results of the last generation run
        /// for the processed configurations
        /// </summary>
        public Dictionary<string, DnaConfiguration> LastGenerationProcessedConfigurations { get; private set; }

        /// <summary>
        /// The results of the last generation run
        /// for the processed results
        /// </summary>
        public List<EngineProcessResult> LastGenerationProcessedResults { get; set; }

        #endregion

        #region Public Events

        /// <summary>
        /// Called when processing of a file succeeded
        /// </summary>
        public event Action<EngineProcessResult> ProcessSuccessful = (result) => { };

        /// <summary>
        /// Called when processing of a file failed
        /// </summary>
        public event Action<EngineProcessResult> ProcessFailed = (result) => { };

        /// <summary>
        /// Called when the engine started
        /// </summary>
        public event Action Started = () => { };

        /// <summary>
        /// Called when the engine stopped
        /// </summary>
        public event Action Stopped = () => { };

        /// <summary>
        /// Called when the engine started watching for a specific file extension
        /// </summary>
        public event Action<string> StartedWatching = (extension) => { };

        /// <summary>
        /// Called when the engine stopped watching for a specific file extension
        /// </summary>
        public event Action<string> StoppedWatching = (extension) => { };

        /// <summary>
        /// Called when a log message is raised
        /// </summary>
        public event Action<LogMessage> LogMessage = (message) => { };

        #endregion

        #region Constructor

        /// <summary>
        /// Default constructor
        /// </summary>
        public BaseEngine()
        {

        }

        #endregion

        #region Processing Methods

        /// <summary>
        /// Any pre-processing to do on a file before any other processing is done
        /// </summary>
        /// <param name="data">The file processing information</param>
        /// <returns></returns>
        protected virtual Task PreProcessFile(FileProcessingData data) => Task.FromResult(0);

        /// <summary>
        /// Any post-processing to do on a file after it has generated the standard output paths
        /// Useful for specifying custom output paths when the standard Dna output tags are not used
        /// such as in the Sass engine where the output paths can come from the Dna Config file
        /// </summary>
        /// <param name="data">The file processing information</param>
        /// <returns></returns>
        protected virtual Task PostProcessOutputPaths(FileProcessingData data) => Task.FromResult(0);

        /// <summary>
        /// Any post-processing to do on a file after any other processing is done
        /// </summary>
        /// <param name="data">The file processing information</param>
        /// <returns></returns>
        protected virtual Task PostProcessFile(FileProcessingData data) => Task.FromResult(0);

        /// <summary>
        /// Any pre-processing to do before generating the output content
        /// </summary>
        /// <param name="data">The file processing information</param>
        /// <param name="output">The file output information</param>
        /// <returns></returns>
        protected virtual Task PreGenerateFile(FileProcessingData data, FileOutputData output) => Task.FromResult(0);

        /// <summary>
        /// Any post-processing to do after generating the output content
        /// </summary>
        /// <param name="data">The file processing information</param>
        /// <param name="output">The file output information</param>
        /// <returns></returns>
        protected virtual Task PostGenerateFile(FileProcessingData data, FileOutputData output) => Task.FromResult(0);

        /// <summary>
        /// Any post-processing to do after the file has been saved
        /// </summary>
        /// <param name="data">The file processing information</param>
        /// <param name="output">The file output information</param>
        /// <returns></returns>
        protected virtual Task PostSaveFile(FileProcessingData data, FileOutputData output) => Task.FromResult(0);

        /// <summary>
        /// The processing action to perform when the given file has been edited
        /// </summary>
        /// <param name="path">The absolute path of the file to process</param>
        /// <param name="generatedFiles">A list of absolute file paths to already generated files in this loop, so they don't get regenerated</param>
        /// <param name="processedFiles">A list of absolute file paths to already processed files in this loop, so they don't get reprocess</param>
        /// <param name="processedConfigurations">A list of absolute file paths to already processed resolved configurations for the folder</param>
        /// <param name="referenceLoopLevel">The nth level deep in a recursive reference loop, indicates this file change has been fired because a file this file references changed, not the file itself</param>
        /// <returns></returns>
        protected async Task<EngineProcessResult> ProcessFileAsync(string path, List<string> generatedFiles, List<string> processedFiles, Dictionary<string, DnaConfiguration> processedConfigurations, int referenceLoopLevel = 0)
        {
            #region Setup Data

            // Prefix reference file processing with > indented to the indentation level
            var logPrefix = (referenceLoopLevel > 0 ? $"{"".PadLeft(referenceLoopLevel * 2, ' ') }> " : "");

            // Process any configuration files for this file
            var processedConfiguration = ProcessConfigurationFiles(path, processedConfigurations);

            // Create new processing data
            var processingData = new FileProcessingData
            {
                FullPath = path,
                LocalConfiguration = processedConfiguration
            };

            #endregion

            #region Read File

            // Make sure the file exists
            if (!FileManager.FileExists(processingData.FullPath))
                return new EngineProcessResult { Success = false, Path = processingData.FullPath, Error = "File no longer exists" };

            // If this file should be read into memory
            if (WillReadFileIntoMemory)
                // Read all the file into memory (it's OK we will never have large files they are text web files)
                processingData.UnprocessedFileContents = await FileManager.ReadAllTextAsync(processingData.FullPath);

            #endregion

            #region Process

            // Skip processing this file if we have already processed it
            if (processedFiles.Any(toSkip => toSkip.EqualsIgnoreCase(processingData.FullPath)) == true)
            {
                Log($"{logPrefix}Skipping already processed file {processingData.FullPath}", type: LogType.Warning);
                return new EngineProcessResult { Success = true, SkippedProcessing = true, Path = path };
            }

            // Pre-processing
            await PreProcessFile(processingData);

            // If it failed
            if (!processingData.Successful)
                // Return the failure
                return new EngineProcessResult { Success = false, Path = path, Error = processingData.Error };

            // If it skipped
            if (processingData.Skip)
            {
                // Log reason
                Log($"{logPrefix}Skipping file {path}...", type: LogType.Warning);
                Log($"{logPrefix}{processingData.SkipMessage}", type: LogType.Warning);

                // Return the failure
                return new EngineProcessResult { Success = true, Path = path };
            }

            // Log the start
            Log($"{logPrefix}Processing file {path}...", type: LogType.Information);

            // Process any Live Variables
            // If we process any, the file will of been updated and saved
            // and will get picked up and re-processed... so return here
            if (WillProcessLiveVariables && await ProcessLiveVariablesAsync(processingData, logPrefix))
                    return new EngineProcessResult { Success = true, Path = path };

            // If we should process the output tags...
            if (WillProcessOutputTags)
            {
                // Process output tags
                ProcessOutputTags(processingData);

                // If it failed
                if (!processingData.Successful)
                    // Return the failure
                    return new EngineProcessResult { Success = false, Path = path, Error = processingData.Error };

                // If it skipped
                if (processingData.Skip)
                {
                    // Log reason
                    Log($"{logPrefix}Skipping file {path}...", type: LogType.Warning);
                    Log($"{logPrefix}{processingData.SkipMessage}", type: LogType.Warning);

                    // Return the failure
                    return new EngineProcessResult { Success = true, Path = path };
                }
            }

            // Any output path processing
            await PostProcessOutputPaths(processingData);

            // If we should process the main tags...
            if (WillProcessMainTags)
            {
                // Process main tags
                ProcessMainTags(processingData);

                // If it failed
                if (!processingData.Successful)
                    // Return the failure
                    return new EngineProcessResult { Success = false, Path = path, Error = processingData.Error };

                // If it skipped
                if (processingData.Skip)
                {
                    // Log reason
                    Log($"{logPrefix}Skipping file {path}...", type: LogType.Warning);
                    Log($"{logPrefix}{processingData.SkipMessage}", type: LogType.Warning);

                    // Return the failure
                    return new EngineProcessResult { Success = true, Path = path };
                }
            }

            // If we should process the data tags...
            if (WillProcessDataTags)
            {
                // Process variables and data
                ProcessDataTags(processingData);

                // If it failed
                if (!processingData.Successful)
                    // If any failed, return the failure
                    return new EngineProcessResult { Success = false, Path = path, Error = processingData.Error };

                // If it skipped
                if (processingData.Skip)
                {
                    // Log reason
                    Log($"{logPrefix}Skipping file {path}...", type: LogType.Warning);
                    Log($"{logPrefix}{processingData.SkipMessage}", type: LogType.Warning);

                    // Return the failure
                    return new EngineProcessResult { Success = true, Path = path };
                }
            }

            // Any post processing
            await PostProcessFile(processingData);

            // If it failed
            if (!processingData.Successful)
                // Return the failure
                return new EngineProcessResult { Success = false, Path = path, Error = processingData.Error };

            // If it skipped
            if (processingData.Skip)
            {
                // Log reason
                Log($"{logPrefix}Skipping file {path}...", type: LogType.Warning);
                Log($"{logPrefix}{processingData.SkipMessage}", type: LogType.Warning);

                // Return the failure
                return new EngineProcessResult { Success = true, Path = path };
            }

            // Now this file is processed, add it to processed list 
            processedFiles.Add(processingData.FullPath);

            #endregion

            #region Generate Outputs

            // All OK, generate files if not a partial file
            if (!processingData.IsPartial)
            {
                // Generate each output
                foreach (var outputPath in processingData.OutputPaths)
                {
                    // Any pre processing
                    await PreGenerateFile(processingData, outputPath);

                    // Skip any files we want to skip
                    if (generatedFiles?.Any(toSkip => toSkip.EqualsIgnoreCase(outputPath.FullPath)) == true)
                    {
                        Log($"{logPrefix}Skipping already generated file {outputPath.FullPath}", type: LogType.Warning);
                        continue;
                    }

                    // Compile output (replace variables with values)
                    GenerateOutput(processingData, outputPath);

                    // If we failed, ignore (it will already be logged)
                    if (!processingData.Successful)
                        continue;

                    // If it skipped
                    if (processingData.Skip)
                    {
                        // Log reason
                        Log($"{logPrefix}Skipping file {path}...", type: LogType.Warning);
                        Log($"{logPrefix}{processingData.SkipMessage}", type: LogType.Warning);

                        // Return the failure
                        continue;
                    }

                    // Any post processing
                    await PostGenerateFile(processingData, outputPath);

                    // Save the contents
                    try
                    {
                        // Save to file
                        await SaveFileContents(processingData, outputPath);

                        // Any pre processing
                        await PostSaveFile(processingData, outputPath);

                        // Add this to the generated list
                        generatedFiles.Add(outputPath.FullPath);

                        // Log it
                        Log($"{logPrefix}Generated file {outputPath.FullPath}", type: LogType.Success);
                    }
                    catch (Exception ex)
                    {
                        // If any failed, return the failure
                        processingData.Error += $"{Environment.NewLine}Error saving generated file {outputPath.FullPath}. {ex.Message}. {System.Environment.NewLine}";
                    }
                }
            }
            else
                // If it is a partial file, log the fact 
                Log($"{logPrefix}Partial file edit {path}...");

            #endregion

            #region Process Referencing Files

            // Search the root monitor folder for all files with the extensions
            // and search within those for a tag that includes this file
            // 
            // Then fire off a process event for each of them
            Log($"{logPrefix}Updating referenced files to {path}...");

            // Find all files references this file
            var filesThatReferenceThisFile = await FindReferencedFilesAsync(path, processingData);

            // If we failed, stop
            if (!processingData.Successful)
                // Return the failure
                return new EngineProcessResult { Success = false, Path = path, Error = processingData.Error };

            // If it skipped
            if (processingData.Skip)
            {
                // Log reason
                Log($"{logPrefix}Skipping file {path}...", type: LogType.Warning);
                Log($"{logPrefix}{processingData.SkipMessage}", type: LogType.Warning);

                // Return the failure
                return new EngineProcessResult { Success = true, Path = path };
            }

            // Process any referenced files
            foreach (var reference in filesThatReferenceThisFile)
            {
                // Process file that referenced partial
                // NOTE: The generatedFiles and processedFiles are references (List's)
                //       so the inner function will add to them the generated and processed files
                //       no need to add them ourselves here
                var result = await ProcessFileChangedAsync(reference, generatedFiles, processedFiles, processedConfigurations, referenceLoopLevel + 1);

                // If a reference fails to process
                // return that result
                if (!result.Success)
                    return result;
            }

            #endregion

            // Log the message
            Log($"{logPrefix}Successfully processed file {path}", type: LogType.Attention);

            // Return result
            return new EngineProcessResult { Success = processingData.Successful, Path = path, GeneratedFiles = generatedFiles.ToArray(), Error = processingData.Error };
        }

        #endregion

        #region Protected Helper Methods

        /// <summary>
        /// Returns an already cached configuration setting for this files directory, or resolves the settings now
        /// </summary>
        /// <param name="filePath">The absolute file path being processed</param>
        /// <param name="processedConfigurations">The list of already processed configuration settings</param>
        /// <returns></returns>
        private DnaConfiguration ProcessConfigurationFiles(string filePath, Dictionary<string, DnaConfiguration> processedConfigurations)
        {
            // Get processed configuration path key (current directory as lower case)
            var processedConfigurationPath = Path.GetDirectoryName(filePath).ToLower();

            // Declare configuration variable
            var processedConfiguration = default(DnaConfiguration);

            // If we have already processed this configuration, get it
            if (processedConfigurations.ContainsKey(processedConfigurationPath))
                processedConfiguration = processedConfigurations[processedConfigurationPath];
            // Otherwise...
            else
            {
                // Get a list of paths to look in for configuration files
                // starting from the monitor folder, going down into the child folder
                var configurationSearchPaths = GetConfigurationSearchPaths(filePath);

                // Resolve the configuration settings
                processedConfiguration = DnaConfiguration.LoadFromFiles(configurationSearchPaths, Path.GetDirectoryName(filePath), DnaEnvironment?.Configuration);

                // Add this to the cached list
                processedConfigurations.Add(processedConfigurationPath, processedConfiguration);
            }

            // Return results
            return processedConfiguration;
        }

        /// <summary>
        /// Takes a path of a file being processed and returns a list of all configuration file paths that should be checked and loaded
        /// </summary>
        /// <param name="path">The path of the file being processed</param>
        /// <returns></returns>
        protected string[] GetConfigurationSearchPaths(string path)
        {
            // Get this files current directory
            var currentDirectory = DnaConfiguration.ResolveFullPath(string.Empty, Path.GetDirectoryName(path), false, out bool wasRelative);

            // If this path is not within the monitor path (somehow?) then just return this folder
            if (!currentDirectory.StartsWith(ResolvedMonitorPath))
            {
                // Break for developer as this is unusual
                Debugger.Break();

                // Return the current files directory path configuration file
                return new[] { Path.Combine(currentDirectory, DnaSettings.ConfigurationFileName) };
            }
            // If this file is within the monitor path (as it should always be)...
            else
            {
                // New list of configuration files
                var configurationFiles = new List<string>();

                // Get all directories until we hit the monitor path
                while (currentDirectory != ResolvedMonitorPath)
                {
                    // Add the current folders configuration file
                    configurationFiles.Add(Path.Combine(currentDirectory, DnaSettings.ConfigurationFileName));

                    // Go up to next folder
                    currentDirectory = Path.GetDirectoryName(currentDirectory);
                }

                // Add the monitor path itself
                configurationFiles.Add(Path.Combine(currentDirectory, DnaSettings.ConfigurationFileName));

                // Reverse order so parents are first and children take priority (load after)
                configurationFiles.Reverse();

                // Return list
                return configurationFiles.ToArray();
            }
        }

        #endregion

        #region Engine Methods

        /// <summary>
        /// Starts the engine ready to handle processing of the specified files
        /// </summary>
        public void Start()
        {
            Log($"{EngineName} Engine Starting...", type: LogType.Information);
            Log("=======================================", type: LogType.Information);

            // TODO: Add async lock here to prevent multiple calls

            // Dispose of any previous engine setup
            Dispose();

            // Make sure we have extensions
            if (EngineExtensions?.Count == 0)
                throw new InvalidOperationException("No engine extensions specified");

            // Let listener know we started
            Started();

            // Log the message
            Log($"Listening to '{ResolvedMonitorPath}'...", type: LogType.Information);
            LogTabbed($"Delay", $"{ProcessDelay}ms", 1);

            // Create a new list of watchers
            mWatchers = new List<FolderWatcher>
            {

                // We need to listen out for file changes per extension
                //EngineExtensions.ForEach(extension => mWatchers.Add(new FolderWatcher
                //{
                //    Filter = "*" + extension,
                //    Path = ResolvedMonitorPath,
                //    UpdateDelay = ProcessDelay
                //}));

                // Add watcher to watch for everything
                new FolderWatcher
                {
                    Filter = "*",
                    Path = ResolvedMonitorPath,
                    UpdateDelay = ProcessDelay
                }
            };

            // Listen on all watchers
            mWatchers.ForEach(watcher =>
            {
                // Listen for file changes
                watcher.FileChanged += Watcher_FileChanged;

                // Listen for deletions
                watcher.FileDeleted += Watcher_FileDeletedAsync;
                watcher.FolderDeleted += Watcher_FolderDeletedAsync;

                // Listen for renames / moves
                watcher.FileRenamed += Watcher_FileRenamedAsync;
                watcher.FolderRenamed += Watcher_FolderRenamedAsync;

                // Inform listener
                StartedWatching(watcher.Filter);

                // Log the message
                LogTabbed($"File Type", watcher.Filter, 1);

                // Start watcher
                watcher.Start();
            });

            // Closing comment tag
            Log("", type: LogType.Information);
        }

        /// <summary>
        /// Performs any startup generation that was specified
        /// </summary>
        public async Task StartupGenerationAsync()
        {
            try
            {
                // If there is nothing to do, just return
                if (DnaEnvironment?.Configuration.GenerateOnStart == GenerateOption.None)
                    return;

                // Update all monitored files
                await FindAllMonitoredFilesAsync();

                // Process files
                await ProcessAllFileChangesAsync(AllMonitoredFiles.Select(f => f.Path).ToList());
            }
            finally
            {
                // Clear processing flag
                Processing = false;
            }
        }

        /// <summary>
        /// Shows a generation report of the last generation 
        /// of the processed results to the log
        /// </summary>
        protected void OutputGenerationReport()
        {
            CoreLogger.LogInformation($"  {EngineName} Generation Report - {LastGenerationGeneratedFiles?.Count} generated files, {LastGenerationProcessedFiles?.Count} processed files");

            // Get any failed results
            var failedFiles = LastGenerationProcessedResults?.Where(result => !result.Success).ToList();
            if (failedFiles.Count > 0)
            {
                // Output title
                CoreLogger.LogInformation("");
                CoreLogger.Log($"{failedFiles.Count} files failed to process", type: LogType.Error);

                // Output each
                failedFiles.ForEach(failedFile =>
                {
                    // Space above
                    CoreLogger.Log("", type: LogType.Error);

                    // File path
                    CoreLogger.LogTabbed($"{failedFile.Path}", string.Empty, 1, LogType.Error);

                    // Error Message
                    CoreLogger.LogTabbed($"{failedFile.Error}", string.Empty, 1, LogType.Error);
                });
            }

            // Spacer
            CoreLogger.LogInformation("");
        }

        #endregion

        #region File Changed

        /// <summary>
        /// Fired when a watcher has detected a file change
        /// </summary>
        /// <param name="path">The path of the file that has changed</param>
        private void Watcher_FileChanged(string path)
        {
            try
            {
                // If we are temporarily ignoring file changes...
                if (DnaEnvironment?.DisableWatching == true)
                    // Return
                    return;

                // Check if this file is a monitored type
                var filename = Path.GetFileName(path);
                if (!EngineExtensions.Any(ex => ex == "*.*" ? true : Regex.IsMatch(filename, ex)))
                    return;

                // For a file change, we want to wait until no more file changes happen
                // for 100ms (otherwise we re-process files lots of times instead of 
                // batching all changes up into one grouped run)

                // Add this file to the list to be processed if not already
                lock (mFilesToProcessLock)
                {
                    // Check if we already have this file in the list to process
                    // NOTE: Case-sensitive check for Linux support
                    if (mFilesToProcess.Any(f => string.Equals(f, path, StringComparison.InvariantCulture)))
                        return;

                    // Add this file to the list to be processed
                    mFilesToProcess.Add(path);

                    // Create new change Id for this call
                    var updateId = Guid.NewGuid();
                    mLastUpdateId = updateId;

                    // Wait the delay period
                    Task.Delay(Math.Max(1, ProcessDelay)).ContinueWith(async (t) =>
                    {
                        // Check if the last update Id still matches
                        // meaning no updates since that time
                        if (updateId != mLastUpdateId)
                            // If there was another change, ignore this one
                            return;

                        // If we are temporarily ignoring file changes...
                        if (DnaEnvironment?.DisableWatching == true)
                            // Return
                            return;

                        // Store files to process in this list
                        var filesToProcess = new List<string>();

                        // Lock, clone and clear process list
                        lock (mFilesToProcessLock)
                        {
                            filesToProcess = mFilesToProcess.ToList();
                            mFilesToProcess = new List<string>();
                        }

                        // Settle time reached, so fire off the change event
                        if (filesToProcess.Count > 0)
                            await ProcessAllFileChangesAsync(filesToProcess);
                    });

                }
            }
            catch (Exception ex)
            {
                CoreLogger.Log($"Unexpected exception in {nameof(Watcher_FileChanged)}. {ex.Message}", type: LogType.Error);
            }
        }

        /// <summary>
        /// Processes all files that have changed since the last process delay
        /// </summary>
        /// <param name="filesToProcess">A list of files to process</param>
        /// <returns></returns>
        protected async Task ProcessAllFileChangesAsync(List<string> filesToProcess)
        {
            try
            {
                // Flag we are processing
                Processing = true;

                // Clear last processed details
                LastGenerationGeneratedFiles = new List<string>();
                LastGenerationProcessedFiles = new List<string>();
                LastGenerationProcessedConfigurations = new Dictionary<string, DnaConfiguration>();
                LastGenerationProcessedResults = new List<EngineProcessResult>();

                // Lock this from running more than one file processing at a time...
                await AsyncAwaitor.AwaitAsync(FileChangeLockKey, async () =>
                {
                    // If we are temporarily ignoring file changes...
                    if (DnaEnvironment?.DisableWatching == true)
                        // Return
                        return;

                    CoreLogger.LogInformation($"====================================");
                    CoreLogger.LogInformation($"  {EngineName} Engine Processing {filesToProcess.Count} File Changes ");
                    CoreLogger.LogInformation($"");

                    // Update all monitored files (used in searching for references)
                    await FindAllMonitoredFilesAsync();

                    // Keep a list of processed and generated files
                    var generatedFiles = new List<string>();
                    var processedFiles = new List<string>();
                    var processedConfigurations = new Dictionary<string, DnaConfiguration>();
                    var processedResults = new List<EngineProcessResult>();

                    foreach (var file in filesToProcess)
                    {
                        // Don't process files twice
                        if (generatedFiles.Any(f => f.EqualsIgnoreCase(file)))
                            continue;

                        // Process file
                        var processedResult = await ProcessFileChangedAsync(file, generatedFiles, processedFiles, processedConfigurations);
                        processedResults.Add(processedResult);
                    };

                    // Set last processed details
                    LastGenerationGeneratedFiles = generatedFiles.ToList();
                    LastGenerationProcessedFiles = processedFiles.ToList();
                    LastGenerationProcessedConfigurations = processedConfigurations;
                    LastGenerationProcessedResults = processedResults.ToList();
                });
            }
            finally
            {

                CoreLogger.LogInformation($"");
                CoreLogger.LogInformation($"  {EngineName} Engine Process Done  ");
                CoreLogger.LogInformation($"====================================");
                CoreLogger.LogInformation($"");

                // Wait for 50ms
                await Task.Delay(50);

                // If all engines are no longer busy, output the generation log
                if (!DnaEnvironment.Engines.Any(engine => engine != this && engine.Processing))
                    // Output the generation report of each engine
                    DnaEnvironment.Engines.ForEach(engine => engine.OutputGenerationReport());

                // Set processing to false
                Processing = false;
            }
        }

        /// <summary>
        /// Called when a file has changed and needs processing
        /// </summary>
        /// <param name="path">The full path of the file to process</param>
        /// <param name="generatedFiles">A list of absolute file paths to already generated files in this loop, so they don't get regenerated</param>
        /// <param name="processedFiles">A list of absolute file paths to already processed files in this loop, so they don't get reprocessed</param>
        /// <param name="processedConfigurations">A list of absolute file paths to already processed resolved configurations for the folder</param>
        /// <param name="referenceLoopLevel">The nth level deep in a recursive reference loop, indicates this file change has been fired because a file this file references changed, not the file itself</param>
        /// <returns></returns>
        protected async Task<EngineProcessResult> ProcessFileChangedAsync(string path, List<string> generatedFiles, List<string> processedFiles, Dictionary<string, DnaConfiguration> processedConfigurations, int referenceLoopLevel = 0)
        {
            // Prefix reference file processing with >
            var logPrefix = (referenceLoopLevel > 0 ? $"{"".PadLeft(referenceLoopLevel * 2)}> " : "");

            try
            {
                // Process the file
                var result = await ProcessFileAsync(path, generatedFiles, processedFiles, processedConfigurations, referenceLoopLevel);

                // Check if we have an unknown response
                if (result == null)
                    throw new ArgumentNullException("Unknown error processing file. No result provided");

                // If we succeeded, let the listeners know
                if (result.Success)
                {
                    // Inform listeners
                    ProcessSuccessful(result);
                }
                // If we failed, let the listeners know
                else
                {
                    // Inform listeners
                    ProcessFailed(result);

                    // Log if this result is for this file
                    // (otherwise it was already logged)
                    if (string.Equals(result.Path, path, StringComparison.InvariantCulture))
                        // Log the message
                        Log($"{logPrefix}Failed to processed file {path}", message: result.Error, type: LogType.Error);
                }

                return result;
            }
            // Catch any unexpected failures
            catch (Exception ex)
            {
                // Create result
                var failedResult = new EngineProcessResult
                {
                    Path = path,
                    Error = ex.Message,
                    Success = false,
                };

                // Generate an unexpected error report
                ProcessFailed(failedResult);

                // Log the message
                Log($"{logPrefix}Unexpected fail to processed file {path}", message: ex.Message, type: LogType.Error);

                // Return the result
                return failedResult;
            }
        }

        #endregion

        #region File/Folder Deleted

        /// <summary>
        /// Fired when the watcher has detected a file deletion
        /// </summary>
        /// <param name="path">The path of the file that has been deleted</param>
        private async void Watcher_FileDeletedAsync(string path)
        {
            try
            {
                // If we are temporarily ignoring file changes...
                if (DnaEnvironment?.DisableWatching == true)
                    // Return
                    return;

                // Lock this from running more than one file processing at a time...
                await AsyncAwaitor.AwaitAsync(FileChangeLockKey, () =>
                {
                    // Process the file deletion
                    return ProcessFileDeletedAsync(path);
                });
            }
            catch (Exception ex)
            {
                CoreLogger.Log($"Unexpected exception in {nameof(Watcher_FileDeletedAsync)}. {ex.Message}", type: LogType.Error);
            }
        }

        /// <summary>
        /// What to do when a watched file is deleted
        /// </summary>
        /// <param name="path">The path to the deleted file</param>
        /// <returns></returns>
        protected virtual Task ProcessFileDeletedAsync(string path)
        {
            // Do nothing by default
            return Task.FromResult(0);
        }

        /// <summary>
        /// Fired when the watcher has detected a folder deletion
        /// </summary>
        /// <param name="path">The path of the folder that has been deleted</param>
        private async void Watcher_FolderDeletedAsync(string path)
        {
            try
            {
                // If we are temporarily ignoring file changes...
                if (DnaEnvironment?.DisableWatching == true)
                    // Return
                    return;

                // Lock this from running more than one file/folder processing at a time...
                await AsyncAwaitor.AwaitAsync(FileChangeLockKey, () =>
                {
                    // Process the folder deletion
                    return ProcessFolderDeletedAsync(path);
                });
            }
            catch (Exception ex)
            {
                CoreLogger.Log($"Unexpected exception in {nameof(Watcher_FolderDeletedAsync)}. {ex.Message}", type: LogType.Error);
            }
        }

        /// <summary>
        /// What to do when a watched folder is deleted
        /// </summary>
        /// <param name="path">The path to the deleted folder</param>
        /// <returns></returns>
        protected virtual Task ProcessFolderDeletedAsync(string path)
        {
            // Do nothing by default
            return Task.FromResult(0);
        }

        #endregion

        #region File/Folder Renamed/Moved

        /// <summary>
        /// Fired when the watcher has detected a file rename/move
        /// </summary>
        /// <param name="details">Details of the file rename/move operation</param>
        private async void Watcher_FileRenamedAsync((string from, string to) details)
        {
            // If we are temporarily ignoring file changes...
            if (DnaEnvironment?.DisableWatching == true)
                // Return
                return;

            // Process file rename
            await ProcessFileRenamedAsync(details);

            // If we should treat a rename as a file change
            if (TreatFileRenameAsChange)
                // And let system know the new file is effectively a change
                Watcher_FileChanged(details.to);
        }

        /// <summary>
        /// Fired when the watcher has detected a file rename/move
        /// </summary>
        /// <param name="details">Details of the file rename/move operation</param>
        /// <returns></returns>
        protected virtual Task ProcessFileRenamedAsync((string from, string to) details)
        {
            // Do nothing by default
            return Task.FromResult(0);
        }

        /// <summary>
        /// Fired when the watcher has detected a folder rename/move
        /// </summary>
        /// <param name="details">Details of the folder rename/move operation</param>
        private async void Watcher_FolderRenamedAsync((string from, string to) details)
        {
            // If we are temporarily ignoring file changes...
            if (DnaEnvironment?.DisableWatching == true)
                // Return
                return;

            // If we should regenerate...
            if (RegenerateOnFolderRename)
                // Run the regeneration
                await StartupGenerationAsync();

            // Call folder rename function
            await ProcessFolderRenamedAsync(details);
        }

        /// <summary>
        /// Fired when the watcher has detected a folder rename/move
        /// </summary>
        /// <param name="details">Details of the folder rename/move operation</param>
        /// <returns></returns>
        protected virtual Task ProcessFolderRenamedAsync((string from, string to) details)
        {
            // Do nothing by default
            return Task.FromResult(0);
        }
        #endregion

        #region Command Tags

        /// <summary>
        /// Processes any Live Variables and updates the actual source file, then carries on the processing
        /// </summary>
        /// <param name="data">The file processing data</param>
        /// <param name="logPrefix">The prefix to any log messages</param>
        private async Task<bool> ProcessLiveVariablesAsync(FileProcessingData data, string logPrefix = "")
        {
            // Create a match variable
            Match match = null;

            // Get a copy of the contents
            var contents = data.UnprocessedFileContents;

            // Keep a flag if we find any matches
            var anyMatch = false;

            // Go though all matches
            while (match == null || match.Success)
            {
                // The next found variable
                LiveDataSourceVariable foundVariable = null;

                // Find next Live Variables
                match = Regex.Match(contents, mLiveVariableUseRegex);

                while (foundVariable == null && match.Success)
                {
                    // Make sure we have enough groups
                    if (match.Groups.Count < 2)
                        continue;

                    // NOTE: Group 0 = $$!VariableHere$$
                    //       Group 1 = VariableHere

                    // Get variable without the surrounding tags $$! $$
                    var variableName = match.Groups[1].Value;

                    // Check if we have a Live Variable
                    foundVariable = DnaEnvironment.LiveDataManager.FindVariable(variableName);

                    // If found live variable...
                    if (foundVariable != null)
                    {
                        // Log it
                        Log($"{logPrefix}Injecting Live Variable '{foundVariable.Name}'");

                        // Replace it with contents
                        ReplaceTag(ref contents, match, await FileManager.ReadAllTextAsync(foundVariable.FilePath), removeNewline: false);

                        // Flag it
                        anyMatch = true;
                    }
                    else
                        // Move to next match
                        match = match.NextMatch();
                }
            }

            // If we got any match, update original source file
            if (anyMatch)
                FileManager.SaveFile(contents, data.FullPath);

            // And update unprocessed data
            data.UnprocessedFileContents = contents;

            // Return if we found any matches and so updated the file
            return anyMatch;
        }

        /// <summary>
        /// Processes the tags and finds all output tags
        /// </summary>
        /// <param name="data">The file processing data</param>
        public void ProcessOutputTags(FileProcessingData data)
        {
            // Find all special tags that have 2 groups
            var match = Regex.Match(data.UnprocessedFileContents, mStandard2GroupRegex, RegexOptions.Singleline);

            // No error to start with
            data.Error = string.Empty;

            //
            // NOTE: Only look for the partial tag on the first run as it must be the top of the file
            //       and after that includes could end up adding partials to the parent confusing the situation
            //
            //       So make sure partials are set at the top of the file
            //
            var firstMatch = true;

            // Store original contents
            var tempContents = data.UnprocessedFileContents;

            // Loop through all matches
            while (match.Success)
            {
                // NOTE: The first group is the full match
                //       The second group and onwards are the matches

                // Make sure we have enough groups
                if (match.Groups.Count < 2)
                {
                    data.Error = $"Malformed match {match.Value}";
                    return;
                }

                // Take the first match as the header for the type of tag
                var tagType = match.Groups[1].Value.ToLower().Trim();

                // Now process each tag type
                switch (tagType)
                {
                    // PARTIAL CLASS
                    case "partial":

                        // Only update flag if it is the first match
                        // so includes don't mess it up
                        if (firstMatch)
                            data.IsPartial = true;

                        // Remove tag
                        ReplaceTag(data, match, string.Empty);

                        break;

                    // OUTPUT NAME
                    case "output":

                        // Make sure we have enough groups
                        if (match.Groups.Count < 3)
                        {
                            data.Error = $"Malformed match {match.Value}";
                            return;
                        }

                        // Get output path
                        var outputPath = match.Groups[2].Value;

                        // Process the output command
                        ProcessOutputTag(data, outputPath, match);

                        if (!data.Successful)
                            // Return false if it fails
                            return;

                        break;

                    // UNKNOWN (just ignore)
                    default:
                        ReplaceTag(data, match, string.Empty);
                        break;
                }

                // Find the next command
                match = Regex.Match(data.UnprocessedFileContents, mStandard2GroupRegex, RegexOptions.Singleline);

                // No longer the first match
                firstMatch = false;
            }

            // Restore contents
            data.UnprocessedFileContents = tempContents;

            // If this isn't a partial class, and we have no outputs specified
            // Create a default one
            if (!data.IsPartial && data.OutputPaths.Count == 0)
            {
                // Get default output name
                data.OutputPaths.Add(new FileOutputData
                {
                    FullPath = GetDefaultOutputPath(data),
                    FileContents = data.UnprocessedFileContents
                });
            }

            // Now set file contents
            data.OutputPaths.ForEach(output => output.FileContents = data.UnprocessedFileContents);
        }

        /// <summary>
        /// Processes an Output name command to add an output path
        /// </summary>
        /// <param name="data">The file processing data</param>
        /// <param name="outputPath">The include path, typically a relative path</param>
        /// <param name="match">The original match that found this information</param>
        /// <returns></returns>
        protected void ProcessOutputTag(FileProcessingData data, string outputPath, Match match)
        {
            // No error to start with
            data.Error = string.Empty;

            // Profile name
            string profileName = null;

            // If the name have a single : then the right half is the profile name
            if (outputPath.Count(c => c == ':') == 1)
            {
                // Set profile path
                profileName = outputPath.Split(':')[1];

                // Set output path
                outputPath = outputPath.Split(':')[0];
            }

            // Add extension if not specified
            if (!Path.HasExtension(outputPath))
                outputPath += OutputExtension;

            // Get the full path from the provided relative path based on the input files location
            var fullPath = DnaConfiguration.ResolveFullPath(data.LocalConfiguration.OutputPath, outputPath, false, out bool wasRelative);

            // Add this to the list
            data.OutputPaths.Add(new FileOutputData
            {
                FullPath = fullPath,
                ProfileName = profileName,
            });

            // Remove the tag
            ReplaceTag(data, match, string.Empty);
        }

        /// <summary>
        /// Processes the tags in the list and edits the files contents as required
        /// </summary>
        /// <param name="data">The file processing data</param>
        public void ProcessMainTags(FileProcessingData data)
        {
            // For each output
            data.OutputPaths.ForEach(output =>
            {
                #region Find Includes 

                // Find all special tags that have 2 groups
                var match = Regex.Match(output.FileContents, mStandard2GroupRegex, RegexOptions.Singleline);

                // No error to start with
                data.Error = string.Empty;

                // Keep track of all includes to monitor for circular references
                var includes = new List<string>();

                // Loop through all matches
                while (match.Success)
                {
                    // NOTE: The first group is the full match
                    //       The second group and onwards are the matches

                    // Make sure we have enough groups
                    if (match.Groups.Count < 2)
                    {
                        data.Error = $"Malformed match {match.Value}";
                        return;
                    }

                    // Take the first match as the header for the type of tag
                    var tagType = match.Groups[1].Value.ToLower().Trim();

                    // Now process each tag type
                    switch (tagType)
                    {
                        // Remove partial and outputs (already processed)
                        case "partial":
                        case "output":
                            ReplaceTag(output, match, string.Empty);
                            break;

                        case "inline":

                            // Make sure we have enough groups
                            if (match.Groups.Count < 3)
                            {
                                data.Error = $"Malformed match {match.Value}";
                                return;
                            }

                            // Get inline data
                            var inlineData = match.Groups[2].Value;

                            // Process the include command
                            ProcessInlineTag(data, output, inlineData, match);

                            if (!data.Successful)
                                // Return false if it fails
                                return;

                            break;

                        // INCLUDE (Replace file)
                        case "include":

                            // Make sure we have enough groups
                            if (match.Groups.Count < 3)
                            {
                                data.Error = $"Malformed match {match.Value}";
                                return;
                            }

                            // Get include path
                            var includePath = match.Groups[2].Value;

                            // NOTE: No need to check includes for circular references as at this level (looping a single file)
                            //       you can include the same file multiple times.
                            //
                            //       A circular reference would happen if an inner include references a file that references itself
                            //       and that we check for in FindReferencedFilesAsync
                            //
                            //       However we do need to check if it includes itself, which we do in the ProcessIncludeTag

                            // Process the include command
                            ProcessIncludeTag(data, output, includePath, match);

                            if (!data.Successful)
                                // Return false if it fails
                                return;

                            // Add this to the list of already processed includes
                            includes.Add(includePath.ToLower().Trim());

                            break;

                        // UNKNOWN
                        default:
                            // Report error of unknown match
                            data.Error = $"Unknown match {match.Value}";
                            return;
                    }

                    // Find the next command
                    match = Regex.Match(output.FileContents, mStandard2GroupRegex, RegexOptions.Singleline);
                }

                #endregion
            });
        }

        /// <summary>
        /// Processes an Inline command to replace a tag with the contents of the data between the tags
        /// </summary>
        /// <param name="data">The file processing data</param>
        /// <param name="output">The file output data</param>
        /// <param name="inlineData">The inline data</param>
        /// <param name="match">The original match that found this information</param>
        protected void ProcessInlineTag(FileProcessingData data, FileOutputData output, string inlineData, Match match)
        {
            // No error to start with
            data.Error = string.Empty;

            // Profile name
            string profileName = null;

            // If the name starts with : then left half is the profile name
            if (inlineData[0] == ':')
            {
                // Set profile path
                // Find first index of a space or newline
                profileName = inlineData.Substring(1, inlineData.IndexOfAny(new[] { ' ', '\r', '\n' }) - 1);

                // Set inline data (+2 to exclude the space after the profile name and the starting :
                inlineData = inlineData.Substring(profileName.Length + 2);
            }

            // NOTE: A blank profile should be included for everything
            //       A ! means only include if no specific profile name is given
            //       Anything else is the a profile name so should only include if matched

            // If the profile is blank, always include it
            if (string.IsNullOrEmpty(profileName) ||
                // Or if we specify ! only include it if the specified profile is  blank
                (profileName == "!" && string.IsNullOrEmpty(output.ProfileName)) ||
                // Or if the profile name matches include it
                output.ProfileName.EqualsIgnoreCase(profileName))
            {
                // Replace the tag with the contents
                ReplaceTag(output, match, inlineData, removeNewline: false);
            }
            // Remove include tag and finish
            else
            {
                ReplaceTag(output, match, string.Empty);
            }
        }

        /// <summary>
        /// Processes an Include command to replace a tag with the contents of another file
        /// </summary>
        /// <param name="data">The file processing data</param>
        /// <param name="output">The file output data</param>
        /// <param name="includePath">The include path, typically a relative path</param>
        /// <param name="match">The original match that found this information</param>
        protected void ProcessIncludeTag(FileProcessingData data, FileOutputData output, string includePath, Match match)
        {
            // No error to start with
            data.Error = string.Empty;

            // Profile name
            string profileName = null;

            // If the name have a single : then the right half is the profile name
            if (includePath.Count(c => c == ':') == 1)
            {
                // Set profile path
                profileName = includePath.Split(':')[1];

                // Set output path
                includePath = includePath.Split(':')[0];
            }

            // If the profile is blank, always include it
            if (string.IsNullOrEmpty(profileName) ||
                // Or if we specify ! only include it if the specified profile is  blank
                (profileName == "!" && string.IsNullOrEmpty(output.ProfileName)) ||
                // Or if the profile name matches include it
                output.ProfileName.EqualsIgnoreCase(profileName))
            {
                // Try and find the include file
                var includedContents = FindIncludeFile(data.FullPath, includePath, out var resolvedPath);

                // If the resolved path is this files path, we have a circular reference
                if (string.Equals(resolvedPath, data.FullPath, StringComparison.InvariantCultureIgnoreCase))
                {
                    data.Error = $"Circular reference detected {resolvedPath}";
                    return;
                }

                // If we didn't find it, error out
                if (includedContents == null)
                {
                    data.Error = $"Include file not found {includePath}";
                    return;
                }

                // Otherwise we got it, so replace the tag with the contents
                ReplaceTag(output, match, includedContents, removeNewline: false);
            }
            // Remove include tag and finish
            else
            {
                ReplaceTag(output, match, string.Empty);
            }
        }

        /// <summary>
        /// Processes the variable and data tags in the list and edits the files contents as required
        /// </summary>
        /// <param name="data">The file processing data</param>
        /// <returns></returns>
        protected void ProcessDataTags(FileProcessingData data)
        {
            // For each output
            data.OutputPaths.ForEach(output =>
            {
                // Find all sets of XML data that contain variables and other data
                var match = Regex.Match(output.FileContents, mStandardVariableRegex, RegexOptions.Singleline);

                // No error to start with
                data.Error = string.Empty;

                // Loop through all matches
                while (match.Success)
                {
                    // NOTE: The first group is the full match
                    //       The second group is the XML

                    // Make sure we have enough groups
                    if (match.Groups.Count < 2)
                    {
                        data.Error = $"Malformed match {match.Value}";
                        return;
                    }

                    // Take the first match as the header for the type of tag
                    var xmlString = match.Groups[1].Value.Trim();

                    // Create XDocument
                    XDocument xmlData = null;

                    try
                    {
                        // Get XML data from it
                        xmlData = XDocument.Parse(xmlString);
                    }
                    catch (Exception ex)
                    {
                        data.Error = $"Malformed data region {xmlString}. {ex.Message}";
                        return;
                    }

                    // Extract variables and any other data from it
                    ExtractData(data, output, xmlData);

                    // If it failed, return now
                    if (!data.Successful)
                        return;

                    // Remove tag
                    ReplaceTag(output, match, string.Empty);

                    // Find the next data region
                    match = Regex.Match(output.FileContents, mStandardVariableRegex, RegexOptions.Singleline);
                }
            });
        }

        #region Private Helpers

        /// <summary>
        /// Searches for an input file in certain locations relative to the input file
        /// and returns the contents of it if found. 
        /// Returns null if not found
        /// </summary>
        /// <param name="path">The input path of the original file</param>
        /// <param name="includePath">The include path of the file trying to be included</param>
        /// <param name="resolvedPath">The resolved full path of the file that was found to be included</param>
        /// <param name="returnContents">True to return the files actual contents, false to return an empty string if found and null otherwise</param>
        /// <returns></returns>
        protected string FindIncludeFile(string path, string includePath, out string resolvedPath, bool returnContents = true)
        {
            // No path yet
            resolvedPath = null;

            // First look in the same folder
            var foundPath = Path.Combine(Path.GetDirectoryName(path), includePath);

            // For each known extension in the environment...
            var allExtensions = DnaEnvironment?.Engines?
                                    // Get each engines extensions
                                    .Select((engine) => engine.EngineExtensions)
                                    .Aggregate((a, b) =>
                                    {
                                        // New combined list
                                        var combined = new List<string>();

                                        // Combine list a
                                        if (a?.Count > 0)
                                            combined.AddRange(a);

                                        // Combine list b
                                        if (b?.Count > 0)
                                            combined.AddRange(b);

                                        // Return combined list
                                        return combined;
                                    })
                                    // Convert to list
                                    .ToList();

            // Loop each known extension...
            foreach (var extension in allExtensions)
            {
                // New variable for expected path
                var newPath = foundPath;

                // If we found it, return contents
                if (FileManager.FileExists(newPath))
                {
                    // Set the resolved path
                    resolvedPath = newPath;

                    // Return the contents
                    return returnContents ? File.ReadAllText(newPath) : string.Empty;
                }

                var underscorePath = string.Empty;

                // Try file with an underscore if it doesn't start with it (as partial files can start with _)
                if (Path.GetFileName(newPath)[0] != '_')
                {
                    underscorePath = Path.Combine(Path.GetDirectoryName(newPath), "_" + Path.GetFileName(newPath));

                    // If we found it, return contents
                    if (FileManager.FileExists(underscorePath))
                    {
                        // Set the resolved path
                        resolvedPath = underscorePath;

                        // Return the contents
                        return returnContents ? File.ReadAllText(underscorePath) : string.Empty;
                    }
                }

                // Add file extension if engine only looks for one extension type
                if (EngineExtensions.Count == 1 && extension != ".*")
                    newPath = newPath + extension;

                // If we found it, return contents
                if (FileManager.FileExists(newPath))
                {
                    // Set the resolved path
                    resolvedPath = newPath;

                    // Return the contents
                    return returnContents ? File.ReadAllText(newPath) : string.Empty;
                }

                // Try file with an underscore if it doesn't start with it (as partial files can start with _)
                if (Path.GetFileName(newPath)[0] != '_')
                {
                    underscorePath = Path.Combine(Path.GetDirectoryName(newPath), "_" + Path.GetFileName(newPath));

                    // If we found it, return contents
                    if (FileManager.FileExists(underscorePath))
                    {
                        // Set the resolved path
                        resolvedPath = underscorePath;

                        // Return the contents
                        return returnContents ? File.ReadAllText(underscorePath) : string.Empty;
                    }
                }

            }

            // Not found
            return null;
        }

        /// <summary>
        /// Replaces a given Regex match with the contents
        /// </summary>
        /// <param name="data">The file output data</param>
        /// <param name="match">The regex match to replace</param>
        /// <param name="newContent">The content to replace the match with</param>
        /// <param name="removeNewline">Remove the newline following the match if one is present</param>
        protected void ReplaceTag(FileOutputData data, Match match, string newContent, bool removeNewline = true)
        {
            var contents = data.FileContents;
            ReplaceTag(ref contents, match, newContent, removeNewline);
            data.FileContents = contents;
        }

        /// <summary>
        /// Replaces a given Regex match with the contents
        /// </summary>
        /// <param name="data">The file processing data</param>
        /// <param name="match">The regex match to replace</param>
        /// <param name="newContent">The content to replace the match with</param>
        /// <param name="removeNewline">Remove the newline following the match if one is present</param>
        protected void ReplaceTag(FileProcessingData data, Match match, string newContent, bool removeNewline = true)
        {
            var contents = data.UnprocessedFileContents;
            ReplaceTag(ref contents, match, newContent, removeNewline);
            data.UnprocessedFileContents = contents;
        }

        /// <summary>
        /// Replaces a given Regex match with the contents
        /// </summary>
        /// <param name="fileContents">The file contents</param>
        /// <param name="match">The regex match to replace</param>
        /// <param name="newContent">The content to replace the match with</param>
        /// <param name="removeNewline">Remove the newline following the match if one is present</param>
        protected void ReplaceTag(ref string fileContents, Match match, string newContent, bool removeNewline = true)
        {
            // If we want to remove a suffixed newline...
            if (removeNewline)
            {
                // Remove carriage return
                if ((fileContents.Length > match.Index + match.Length) &&
                    fileContents[match.Index + match.Length] == '\r')
                    fileContents = string.Concat(fileContents.Substring(0, match.Index + match.Length), fileContents.Substring(match.Index + match.Length + 1));

                // Return newline
                if ((fileContents.Length > match.Index + match.Length) &&
                    fileContents[match.Index + match.Length] == '\n')
                    fileContents = string.Concat(fileContents.Substring(0, match.Index + match.Length), fileContents.Substring(match.Index + match.Length + 1));
            }

            // If the match is at the start, replace it
            if (match.Index == 0)
                fileContents = newContent + fileContents.Substring(match.Length);
            // Otherwise do an inner replace
            else
                fileContents = string.Concat(fileContents.Substring(0, match.Index), newContent, fileContents.Substring(match.Index + match.Length));
        }

        #endregion

        #endregion

        #region Generate Output

        /// <summary>
        /// Replaces all variables with the variable values and generates the final output data
        /// for a given output path
        /// </summary>
        /// <param name="data">The file processing data</param>
        /// <param name="output">The output data to generate the contents for</param>
        protected virtual void GenerateOutput(FileProcessingData data, FileOutputData output)
        {
            // Create a match variable
            Match match = null;

            // Get a copy of the contents
            var contents = output.FileContents;

            // Go though all matches
            while (WillProcessVariables && (match == null || match.Success))
            {
                // Find all variables
                match = Regex.Match(contents, mVariableUseRegex);

                // Make sure we have enough groups
                if (match.Groups.Count < 2)
                    continue;

                // NOTE: Group 0 = $$VariableHere$$
                //       Group 1 = VariableHere

                // Get variable without the surrounding tags $$
                var variable = match.Groups[1].Value;

                // Check if we have a Dna Variable
                if (variable.StartsWith(mDnaVariablePrefix))
                {
                    // If it fails to process, return now (don't output)
                    if (!ProcessDnaVariable(data, output, match, ref contents))
                        return;
                }
                // Otherwise...
                else
                {
                    // If it fails to process, return now (don't output)
                    if (!ProcessVariable(data, output, match, ref contents))
                        return;
                }
            }

            // Set results
            output.CompiledContents = contents;
        }

        /// <summary>
        /// Processes the Dna variable and converts it into the resolved value
        /// </summary>
        /// <param name="data">The processing data</param>
        /// <param name="output">The file output data</param>
        /// <param name="match">The regex match that found this variable</param>
        /// <param name="contents">The files original contents</param>
        private bool ProcessDnaVariable(FileProcessingData data, FileOutputData output, Match match, ref string contents)
        {
            // Get the variable name (without the dna. prefix as well)
            var variable = match.Groups[1].Value.Substring(mDnaVariablePrefix.Length);

            // Date Time
            var dateMatch = Regex.Match(contents, mDnaVariableDateRegex, RegexOptions.Singleline);
            if (dateMatch.Success && dateMatch.Groups.Count >= 2)
            {
                // Get the string format from inside Date("")
                var dateFormat = dateMatch.Groups[1].Value;

                // Now try and replace the value, catching any unexpected errors
                return TryProcessDnaVariable(data, output, match, ref contents, variable, () =>
                {
                    return DateTime.Now.ToString(dateFormat);
                });
            }
            // Project Path
            else if (variable.EqualsIgnoreCase(mDnaVariableProjectPath))
            {
                // Now try and replace the value, catching any unexpected errors
                return TryProcessDnaVariable(data, output, match, ref contents, variable, () =>
                {
                    return System.Environment.CurrentDirectory;
                });
            }
            // File Path
            else if (variable.EqualsIgnoreCase(mDnaVariableFilePath))
            {
                // Now try and replace the value, catching any unexpected errors
                return TryProcessDnaVariable(data, output, match, ref contents, variable, () =>
                {
                    return data.FullPath;
                });
            }
            // Error if we don't get one
            else
            {
                data.Error = $"Dna Variable not found {variable}";

                // Clear contents as the output will be invalid now if it is not processable
                output.CompiledContents = null;

                return false;
            }
        }

        /// <summary>
        /// Tries to process a Dna Variable, catching any unexpected errors and handling them nicely
        /// </summary>
        /// <param name="data">The file processing data</param>
        /// <param name="output">The file output data</param>
        /// <param name="match">The regex match that foudn this Dna variable</param>
        /// <param name="contents">The current file contents</param>
        /// <param name="variable">The variable name being processed</param>
        /// <param name="process">The action to run</param>
        /// <returns></returns>
        private bool TryProcessDnaVariable(FileProcessingData data, FileOutputData output, Match match, ref string contents, string variable, Func<string> process)
        {
            try
            {
                // Try and get the expected value to replace from the action
                var result = process();

                // Now replace it
                ReplaceTag(ref contents, match, result);

                return true;
            }
            catch (Exception ex)
            {
                data.Error = $"Unexpected error processing Dna Variable {variable}.{Environment.NewLine}{ex.Message}";

                // Clear contents as the output will be invalid now if it is not processable
                output.CompiledContents = null;

                return false;
            }
        }

        /// <summary>
        /// Processes the variable and converts it into the value of that variable
        /// </summary>
        /// <param name="data">The processing data</param>
        /// <param name="output">The file output data</param>
        /// <param name="match">The regex match that found this variable</param>
        /// <param name="contents">The files original contents</param>
        private bool ProcessVariable(FileProcessingData data, FileOutputData output, Match match, ref string contents)
        {
            // Get the variable name
            var variable = match.Groups[1].Value;

            // Resolve the name to a variable value stored in the output variables
            var variableValue = output.Variables.FirstOrDefault(v =>
                v.Name.EqualsIgnoreCase(variable) &&
                v.ProfileName.EqualsIgnoreCase(output.ProfileName));

            // If this was a profile-specific variable, fallback to the standard variable 
            if (variableValue == null && !string.IsNullOrEmpty(output.ProfileName))
                variableValue = output.Variables.FirstOrDefault(v =>
                    v.Name.EqualsIgnoreCase(variable) &&
                    string.IsNullOrEmpty(v.ProfileName));

            // Error if we don't get one
            if (variableValue == null)
            {
                data.Error = $"Variable not found {variable} for profile '{output.ProfileName}'";

                // Clear contents as the output will be invalid now if it is not processable
                output.CompiledContents = null;
                return false;
            }

            // Replace with the value
            ReplaceTag(ref contents, match, variableValue?.Value);

            return true;
        }

        /// <summary>
        /// Changes the file extension to the default output file extension
        /// </summary>
        /// <param name="data">The file processing data</param>
        /// <returns></returns>
        protected string GetDefaultOutputPath(FileProcessingData data)
        {
            return Path.Combine(data.LocalConfiguration.OutputPath, OutputExtension == null ? Path.GetFileName(data.FullPath) : Path.GetFileNameWithoutExtension(data.FullPath) + OutputExtension);
        }

        #endregion

        #region Save Output

        /// <summary>
        /// Saves the files compiled contents to the output location
        /// </summary>
        /// <param name="processingData">The processing data</param>
        /// <param name="outputPath">The output path information</param>
        /// <returns></returns>
        public virtual Task SaveFileContents(FileProcessingData processingData, FileOutputData outputPath)
        {
            return SafeTask.Run(() =>
            {
                // Save the contents
                FileManager.SaveFile(outputPath.CompiledContents, outputPath.FullPath);
            });
        }

        #endregion

        #region Extract Data Regions

        /// <summary>
        /// Extracts the data from an <see cref="XDocument"/> and stores the variables and data in the engine
        /// </summary>
        /// <param name="data">The file processing data</param>
        /// <param name="output">The file output data</param>
        /// <param name="xmlData">The data to extract</param>
        protected void ExtractData(FileProcessingData data, FileOutputData output, XDocument xmlData)
        {
            // Find any variables
            ExtractVariables(data, output, xmlData.Root);

            // Profiles
            foreach (var profileElement in xmlData.Root.Elements("Profile"))
            {
                // Find any variables
                ExtractVariables(data, output, profileElement, profileName: profileElement.Attribute("Name")?.Value);
            }

            // Groups
            foreach (var groupElement in xmlData.Root.Elements("Group"))
            {
                // Find any variables
                ExtractVariables(data, output, groupElement, profileName: groupElement.Attribute("Profile")?.Value, groupName: groupElement.Attribute("Name")?.Value);
            }
        }

        /// <summary>
        /// Extract variables from the XML element
        /// </summary>
        /// <param name="data">The file processing data</param>
        /// <param name="output">The file output data</param>
        /// <param name="element">The Xml element</param>
        /// <param name="profileName">The profile name, if explicitly specified</param>
        protected void ExtractVariables(FileProcessingData data, FileOutputData output, XElement element, string profileName = null, string groupName = null)
        {
            // Loop all elements with the name of variable
            foreach (var variableElement in element.Elements("Variable"))
            {
                try
                {
                    // Create the variable
                    var variable = new EngineVariable
                    {
                        XmlElement = variableElement,
                        Name = variableElement.Attribute("Name")?.Value,
                        ProfileName = variableElement.Attribute("Profile")?.Value ?? profileName,
                        Group = variableElement.Attribute("Group")?.Value ?? groupName,
                        Value = variableElement.Element("Value")?.Value ?? variableElement.Value,
                        Comment = variableElement.Element("Comment")?.Value ?? variableElement.Attribute("Comment")?.Value
                    };

                    // Convert string empty profile names back to null
                    if (variable.ProfileName == string.Empty)
                        variable.ProfileName = null;

                    // If we have no comment, look at previous element for a comment
                    if (string.IsNullOrEmpty(variable.Comment) && variableElement.PreviousNode is XComment)
                        variable.Comment = ((XComment)variableElement.PreviousNode).Value;

                    // Make sure we got a name at least
                    if (string.IsNullOrEmpty(variable.Name))
                    {
                        data.Error = $"Variable has no name {variableElement}";
                        break;
                    }

                    // Add or update variable
                    var existing = output.Variables.FirstOrDefault(f =>
                        f.Name.EqualsIgnoreCase(variable.Name) &&
                        f.ProfileName.EqualsIgnoreCase(variable.ProfileName));

                    // If one exists, update it
                    if (existing != null)
                        existing.Value = variable.Value;
                    // Otherwise, add it
                    else
                        output.Variables.Add(variable);
                }
                catch (Exception ex)
                {
                    data.Error = $"Unexpected error parsing variable {variableElement}. {ex.Message}";

                    // No more processing
                    break;
                }
            }
        }

        #endregion

        #region Find References

        /// <summary>
        /// Searches the <see cref="MonitorPath"/> for all files that match the <see cref="EngineExtensions"/> 
        /// then searches inside them to see if they include the includePath passed in
        /// </summary>
        /// <param name="includePath">The path to look for being included in any of the files</param>
        /// <param name="data">The file processing data</param>
        /// <param name="existingReferences">A list of already found references to check for circular references</param>
        /// <returns></returns>
        protected virtual async Task<List<string>> FindReferencedFilesAsync(string includePath, FileProcessingData data, List<string> existingReferences = null)
        {
            #region Setup Data

            // New empty list
            var toProcess = new List<string>();

            // Make existing references list if not one
            if (existingReferences == null)
                existingReferences = new List<string>();

            // New list for any reference files found
            var filesThatReferenceThisFile = new List<string>();

            #endregion

            // If we have no path, return 
            if (string.IsNullOrWhiteSpace(includePath))
                return toProcess;

            #region Find Files That Reference This File

            // Find all files in the monitor path
            var allFiles = AllMonitoredFiles;

            // For each file, find all resolved references
            foreach (var file in allFiles)
            {
                // If any match this file...
                if (file.References.Any(reference => reference.EqualsIgnoreCase(includePath)))
                {
                    // Add this file to be processed
                    toProcess.Add(file.Path);

                    // Add as a parent to check
                    filesThatReferenceThisFile.Add(file.Path);
                }
            }

            #endregion

            #region Circular Reference Check 

            // Add this files own references to the list of references we have found so far as we step up the tree
            existingReferences.AddRange(await GetResolvedIncludePathsAsync(includePath));

            // Circular reference check
            if (existingReferences.Contains(includePath))
            {
                data.Error = $"Circular reference detected to {includePath}";
                return toProcess;
            }

            #endregion

            #region Recursive Step-Up Loop

            // Now recursively loop all parents looking for any files that reference them
            foreach (var referencedFile in filesThatReferenceThisFile)
            {
                // Get all files that reference this parent
                // NOTE: Don't pass the existing references as a reference for referenced files
                //       Their own references are not related to each other parent reference
                var parentReferences = await FindReferencedFilesAsync(referencedFile, data, new List<string>(existingReferences));

                // Add them to the list
                foreach (var parentReference in parentReferences)
                {
                    // Add this to the list
                    toProcess.Add(parentReference);
                }
            }

            #endregion

            // Return what we found
            return toProcess;
        }

        /// <summary>
        /// Finds all files in the monitor folder that match this engines extension types
        /// </summary>
        /// <returns></returns>
        protected async Task FindAllMonitoredFilesAsync()
        {
            // Clear previous list
            AllMonitoredFiles.Clear();

            // Get all monitored files
            var monitoredFiles = FileHelpers.GetDirectoryFiles(ResolvedMonitorPath, "*.*")
                           .Where(file => EngineExtensions.Any(ex => ex == "*.*" ? true : Regex.IsMatch(Path.GetFileName(file), ex)))
                           .Distinct()
                           .ToList();

            // For each find their references
            foreach (var file in monitoredFiles)
            {
                // Get all resolved references
                var references = await GetResolvedIncludePathsAsync(file);

                // Add to list
                AllMonitoredFiles.Add((file, references));
            }
        }

        /// <summary>
        /// Returns a list of resolved paths for all include files in a file
        /// </summary>
        /// <param name="filePath">The full path to the file to check</param>
        /// <returns></returns>
        protected virtual async Task<List<string>> GetResolvedIncludePathsAsync(string filePath)
        {
            // New blank list
            var paths = new List<string>();

            // Make sure the file exists
            if (!FileManager.FileExists(filePath))
                return paths;

            // Read all the file into memory (it's ok we will never have large files they are text web files)
            var fileContents = await FileManager.ReadAllTextAsync(filePath);

            // Create a match variable
            Match match = null;

            // Go though all matches
            while (match == null || match.Success)
            {
                // If we have already run a match once...
                if (match != null)
                    // Remove previous tag and carry on
                    ReplaceTag(ref fileContents, match, string.Empty);

                // Find all special tags that have 2 groups
                if (!GetIncludeTag(filePath, fileContents, ref match, out List<string> includePaths))
                    continue;

                // For each include path found in the match
                includePaths.ForEach(includePath =>
                {
                    // Strip any profile name (we don't care about that for this)
                    // If the name have a single : then the right half is the profile name
                    if (includePath.Count(c => c == ':') == 1)
                    {
                        // Make sure this isn't from an absolute path like C:\Some...
                        var index = includePath.IndexOf(':');
                        if (includePath.Length <= index + 1 || !(includePath[index + 1] == '\\' || includePath[index + 1] == '/'))
                            includePath = includePath.Split(':')[0];
                    }

                    // Resolve any relative aspects of the path
                    includePath = DnaConfiguration.ResolveFullPath(Path.GetDirectoryName(filePath), includePath, false, out bool wasRelative);

                    // Try and find the include file
                    FindIncludeFile(filePath, includePath, out string resolvedPath, returnContents: false);

                    // Add the resolved path if we got one
                    if (!string.IsNullOrEmpty(resolvedPath))
                        paths.Add(resolvedPath);
                });
            }

            // Return the results
            return paths;
        }

        /// <summary>
        /// Searches the fileContents for an include statement and returns its 
        /// </summary>
        /// <param name="fileContents">The path of the file to look in</param>
        /// <param name="fileContents">The file contents to search</param>
        /// <param name="match">The match used that found the include (must be provided in order to remove the found include statement from the file contents)</param>
        /// <param name="includePaths">The include path(s) found</param>
        /// <returns></returns>
        protected virtual bool GetIncludeTag(string filePath, string fileContents, ref Match match, out List<string> includePaths)
        {
            // Blank list start with
            includePaths = new List<string>();

            // Try and find match
            match = Regex.Match(fileContents, mStandard2GroupRegex, RegexOptions.Singleline);

            // Make sure we have enough groups
            if (match.Groups.Count < 3)
                return false;

            // Make sure this is an include
            if (!match.Groups[1].Value.EqualsIgnoreCase("include"))
                return false;

            // Get include path value
            includePaths.Add(match.Groups[2].Value);

            // Return successful
            return true;
        }

        #endregion

        #region Logger

        /// <summary>
        /// Logs a message and raises the <see cref="LogMessage"/> event
        /// </summary>
        /// <param name="title">The title of the log</param>
        /// <param name="message">The main message of the log</param>
        /// <param name="type">The type of the log message</param>
        public void Log(string title, string message = "", LogType type = LogType.Diagnostic)
        {
            LogMessage(new LogMessage
            {
                Title = title,
                Message = message,
                Time = DateTime.UtcNow,
                Type = type
            });
        }

        /// <summary>
        /// Logs a message and raises the <see cref="LogMessage"/> event
        /// Logs a name and value at a certain tab level to simulate a sub-item of another log
        /// 
        /// For example: LogTabbed("Name", "Value", 1);
        /// ----Name: Value
        /// Where ---- are spaces
        /// </summary>
        /// <param name="name">The name to log</param>
        /// <param name="value">The value to log</param>
        /// <param name="tabLevel"></param>
        /// <param name="type">The type of the log message</param>
        public void LogTabbed(string name, string value, int tabLevel, LogType type = LogType.Diagnostic)
        {
            // Add 4 spaces per tab level
            Log($"{"".PadLeft(tabLevel * 4, ' ')}{name}: {value}", type: type);
        }

        #endregion

        #region Dispose

        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {
            // Log the message
            if (mWatchers != null)
                Log($"{EngineName} Engine stopped");

            // Clean up all file watchers
            mWatchers?.ForEach(watcher =>
            {
                // Get extension
                var extension = watcher.Filter;

                // Dispose of watcher
                watcher.Dispose();

                // Inform listener
                StoppedWatching(extension);

                // Log the type
                LogTabbed("File Type", watcher.Filter, 1);
            });

            // Space between each engine log
            Log("");

            // Let listener know we stopped
            if (mWatchers != null)
                Stopped();

            // Clear watchers
            mWatchers = null;
        }

        #endregion
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Dna.HtmlEngine.Core
{
    /// <summary>
    /// A base engine that any specific engine should implement
    /// </summary>
    public abstract class BaseEngine : IDisposable
    {
        #region Protected Members

        /// <summary>
        /// A list of folder watchers that listen out for file changes of the given extensions
        /// </summary>
        protected List<FolderWatcher> mWatchers;

        /// <summary>
        /// The regex to match special tags containing up to 2 values
        /// For example: <!--@ include header @--> to include the file header._dnaweb or header.dnaweb if found
        /// </summary>
        protected string mStandard2GroupRegex = @"<!--@\s*(\w+)\s*(.*?)\s*@-->";

        /// <summary>
        /// The regex to match special tags containing variables and data (which are stored as XML inside the tag)
        /// </summary>
        protected string mStandardVariableRegex = @"<!--\$(.*)\$-->";

        /// <summary>
        /// The regex used to find variables to be replaced with the values
        /// </summary>
        protected string mVariableUseRegex = @"\$\$(.*)\$\$";

        #endregion

        #region Public Properties

        /// <summary>
        /// The paths to monitor for files
        /// </summary>
        public string MonitorPath { get; set; }

        /// <summary>
        /// The desired default output extension for generated files if not overridden
        /// </summary>
        public string OutputExtension { get; set; } = ".dna";

        /// <summary>
        /// The time in milliseconds to wait for file edits to stop occurring before processing the file
        /// </summary>
        public int ProcessDelay { get; set; } = 100;

        /// <summary>
        /// The filename extensions to monitor for
        /// All files: .*
        /// Specific types: .dnaweb
        /// </summary>
        public List<string> EngineExtensions { get; set; }

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

        #region Processing Methods

        /// <summary>
        /// Any pre-processing to do on a file before any other processing is done
        /// </summary>
        /// <param name="data">The file processing information</param>
        /// <returns></returns>
        protected virtual Task PreProcessFile(FileProcessingData data) => Task.FromResult(0);

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
        /// <param name="path"></param>
        /// <returns></returns>
        protected async Task<EngineProcessResult> ProcessFile(string path)
        {
            // Create new processing data
            var processingData = new FileProcessingData { FullPath = path };

            // Make sure the file exists
            if (!FileManager.FileExists(processingData.FullPath))
                return new EngineProcessResult { Success = false, Path = processingData.FullPath, Error = "File no longer exists" };

            // Read all the file into memory (it's ok we will never have large files they are text web files)
            processingData.UnprocessedFileContents = FileManager.ReadAllText(processingData.FullPath);

            // Pre-processing
            await PreProcessFile(processingData);

            // If it failed
            if (!processingData.Successful)
                // Return the failure
                return new EngineProcessResult { Success = false, Path = path, Error = processingData.Error };

            // Find all outputs
            ProcessOutputTags(processingData);

            // If it failed
            if (!processingData.Successful)
                // Return the failure
                return new EngineProcessResult { Success = false, Path = path, Error = processingData.Error };

            // Process base tags
            ProcessMainTags(processingData);

            // If it failed
            if (!processingData.Successful)
                // Return the failure
                return new EngineProcessResult { Success = false, Path = path, Error = processingData.Error };

            // Process variables and data
            ProcessDataTags(processingData);

            // If it failed
            if (!processingData.Successful)
                // If any failed, return the failure
                return new EngineProcessResult { Success = false, Path = path, Error = processingData.Error };

            // Any post processing
            await PostProcessFile(processingData);

            // If it failed
            if (!processingData.Successful)
                // Return the failure
                return new EngineProcessResult { Success = false, Path = path, Error = processingData.Error };

            // All ok, generate HTML files if not a partial file

            if (!processingData.IsPartial)
            {
                // Generate each output
                processingData.OutputPaths.ForEach(async (outputPath) =>
                {
                    // Any pre processing
                    await PreGenerateFile(processingData, outputPath);

                    // Compile output (replace variables with values)
                    GenerateOutput(processingData, outputPath);

                    // If we failed, ignore (it will already be logged)
                    if (!processingData.Successful)
                        return;

                    // Any post processing
                    await PostGenerateFile(processingData, outputPath);

                    // Save the contents
                    try
                    {
                        // Save the contents
                        FileManager.SaveFile(outputPath.CompiledContents, outputPath.FullPath);

                        // Any pre processing
                        await PostSaveFile(processingData, outputPath);

                        // Log it
                        Log($"Generated file {outputPath.FullPath}");
                    }
                    catch (Exception ex)
                    {
                        // If any failed, return the failure
                        processingData.Error += $"{System.Environment.NewLine}Error saving generated file {outputPath.FullPath}. {ex.Message}. {System.Environment.NewLine}";
                    }
                });
            }
            else
            {
                // If it is a partial file, searc the root monitor folder for all files with the extensions
                // and search within those for a tag that includes this partial fie
                // 
                // Then fire off a process event for each of them
                Log($"Partial file edit. Updating referenced files to {path}...");

                // Find all files references this path
                var referencedFiles = FindReferencedFiles(path);

                // Process any referenced files
                foreach (var reference in referencedFiles)
                    await ProcessFileChanged(reference);
            }

            // Return result
            return new EngineProcessResult { Success = processingData.Successful, Path = path, Error = processingData.Error };
        }

        #endregion

        #region Constructor

        /// <summary>
        /// Default constructor
        /// </summary>
        public BaseEngine()
        {

        }

        #endregion

        #region Engine Methods

        /// <summary>
        /// Starts the engine ready to handle processing of the specified files
        /// </summary>
        public void Start()
        {
            // Lock this class so only one call can happen at a time
            lock (this)
            {
                // Dipose of any previous engine setup
                Dispose();

                // Make sure we have extensions
                if (this.EngineExtensions?.Count == 0)
                    throw new InvalidOperationException("No engine extensions specified");

                // Load settings
                LoadSettings();

                // Resolve path
                if (!Path.IsPathRooted(MonitorPath))
                    MonitorPath = Path.GetFullPath(Path.Combine(System.AppContext.BaseDirectory, MonitorPath));

                // Let listener know we started
                Started();

                // Log the message
                Log($"Engine started listening to '{this.MonitorPath}' with {this.ProcessDelay}ms delay...");
                    
                // Create a new list of watchers
                mWatchers = new List<FolderWatcher>();

                // We need to listen out for file changes per extension
                EngineExtensions.ForEach(extension => mWatchers.Add(new FolderWatcher
                {
                    Filter = "*" + extension, 
                    Path = MonitorPath,
                    UpdateDelay = ProcessDelay
                }));

                // Listen on all watchers
                mWatchers.ForEach(watcher =>
                {
                    // Listen for file changes
                    watcher.FileChanged += Watcher_FileChanged;

                    // Inform listener
                    StartedWatching(watcher.Filter);

                    // Log the message
                    Log($"Engine listening for file type {watcher.Filter}");

                    // Start watcher
                    watcher.Start();
                });
            }
        }

        /// <summary>
        /// Loads settings from a dna.config file
        /// </summary>
        private void LoadSettings()
        {
            // Default monitor path of this folder
            MonitorPath = System.AppContext.BaseDirectory;

            // Read config file for monitor path
            try
            {
                var configFile = Path.Combine(System.AppContext.BaseDirectory, "dna.config");
                if (File.Exists(configFile))
                {
                    var configData = File.ReadAllLines(configFile);

                    // Try and find line starting with monitor: 
                    var monitor = configData.FirstOrDefault(f => f.StartsWith("monitor: "));

                    // If we didn't find it, ignore
                    if (monitor == null)
                        return;

                    // Otherwise, load the monitor path
                    monitor = monitor.Substring("monitor: ".Length);

                    // Convert path to full path
                    if (Path.IsPathRooted(monitor))
                        MonitorPath = monitor;
                    // Else resolve the relative path
                    else
                        MonitorPath = Path.GetFullPath(Path.Combine(System.AppContext.BaseDirectory, monitor));

                    // Log it
                    Log($"Monitor path set to: {MonitorPath}");
                }
            }
            // Don't care about config file failures other than logging it
            catch (Exception ex)
            {
                Log("Failed to read or process dna.config file", message: ex.Message, type: LogType.Warning);
            }
        }

        #endregion

        #region File Changed

        /// <summary>
        /// Fired when a watcher has detected a file change
        /// </summary>
        /// <param name="path">The path of the file that has changed</param>
        private async void Watcher_FileChanged(string path)
        {
            // Process the file
            await ProcessFileChanged(path);
        }

        /// <summary>
        /// Called when a file has changed and needs processing
        /// </summary>
        /// <param name="path">The full path of the file to process</param>
        /// <returns></returns>
        protected async Task ProcessFileChanged(string path)
        {
            try
            {
                // Log the start
                Log($"Processing file {path}...", type: LogType.Information);

                // Process the file
                var result = await ProcessFile(path);

                // Check if we have an unknown response
                if (result == null)
                    throw new ArgumentNullException("Unknown error processing file. No result provided");

                // If we succeeded, let the listeners know
                if (result.Success)
                {
                    // Inform listeners
                    ProcessSuccessful(result);

                    // Log the message
                    Log($"Successfully processed file {path}", type: LogType.Success);
                }
                // If we failed, let the listeners know
                else
                {
                    // Inform listeners
                    ProcessFailed(result);

                    // Log the message
                    Log($"Failed to processed file {path}", message: result.Error, type: LogType.Error);
                }
            }
            // Catch any unexpected failures
            catch (Exception ex)
            {
                // Generate an unexpected error report
                ProcessFailed(new EngineProcessResult
                {
                    Path = path,
                    Error = ex.Message,
                    Success = false,
                });

                // Log the message
                Log($"Unexpected fail to processed file {path}", message: ex.Message, type: LogType.Error);
            }
        }

        #endregion

        #region Command Tags

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
                    FullPath = GetDefaultOutputPath(data.FullPath),
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

            // Get the full path from the provided relative path based on the input files location
            var fullPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(data.FullPath), outputPath));

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

                            // Make sure we have not already included it in this run
                            if (includes.Contains(includePath.ToLower().Trim()))
                            {
                                data.Error = $"Circular reference detected {includePath}";
                                return;
                            }

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
                profileName = inlineData.Substring(1, inlineData.IndexOfAny(new [] { ' ', '\r', '\n' }) - 1);

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
                string.Equals(output.ProfileName, profileName, StringComparison.CurrentCultureIgnoreCase))
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
                string.Equals(output.ProfileName, profileName, StringComparison.CurrentCultureIgnoreCase))
            {
                // Try and find the include file
                var includedContents = FindIncludeFile(data.FullPath, includePath, out string resolvedPath);

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
        /// <param name="path">The input path of the orignal file</param>
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

            // If we found it, return contents
            if (FileManager.FileExists(foundPath))
            {
                // Set the resolved path
                resolvedPath = foundPath;

                // Return the contents
                return returnContents ? File.ReadAllText(foundPath) : string.Empty;
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
        protected void GenerateOutput(FileProcessingData data, FileOutputData output)
        {
            // Create a match variable
            Match match = null;

            // Get a copy of the contents
            var contents = output.FileContents;

            // Go though all matches
            while (match == null || match.Success)
            {
                // Find all variables
                match = Regex.Match(contents, mVariableUseRegex);

                // Make sure we have enough groups
                if (match.Groups.Count < 2)
                    continue;

                // Get include path
                var variable = match.Groups[1].Value;

                // Get the variable 
                var variableValue = output.Variables.FirstOrDefault(v =>
                    string.Equals(v.Name, variable, StringComparison.CurrentCultureIgnoreCase) &&
                    string.Equals(v.ProfileName, output.ProfileName, StringComparison.CurrentCultureIgnoreCase));

                // If this was a profile-specific variable, fallback to the standard variable 
                if (variableValue == null && !string.IsNullOrEmpty(output.ProfileName))
                    variableValue = output.Variables.FirstOrDefault(v =>
                        string.Equals(v.Name, variable, StringComparison.CurrentCultureIgnoreCase) &&
                        string.IsNullOrEmpty(v.ProfileName));

                // Error if we don't get one
                if (variableValue == null)
                {
                    data.Error = $"Variable not found {variable} for profile '{output.ProfileName}'";
                    output.CompiledContents = null;
                    return;
                }

                // Replace with the value
                ReplaceTag(ref contents, match, variableValue?.Value);
            }

            // Set results
            output.CompiledContents = contents;
        }

        /// <summary>
        /// Changes the file extension to the default output file extension
        /// </summary>
        /// <param name="path">The full path to the file</param>
        /// <returns></returns>
        protected string GetDefaultOutputPath(string path)
        {
            return Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path) + this.OutputExtension);
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
                        string.Equals(f.Name, variable.Name) &&
                        string.Equals(f.ProfileName, variable.ProfileName));

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
        /// then searches inside them to see if they include the includePath passed in />
        /// </summary>
        /// <param name="includePath">The path to look for being included in any of the files</param>
        /// <returns></returns>
        protected List<string> FindReferencedFiles(string includePath)
        {
            // New empty list
            var toProcess = new List<string>();

            // If we have no path, return 
            if (string.IsNullOrWhiteSpace(includePath))
                return toProcess;

            // Find all files in the monitor path
            var allFiles = Directory.EnumerateFiles(this.MonitorPath, "*.*", SearchOption.AllDirectories)
                .Where(file => this.EngineExtensions.Any(ex => Regex.IsMatch(Path.GetFileName(file), ex)))
                .ToList();
            
            // For each file, find all resolved references
            allFiles.ForEach(file =>
            {
                // Get all resolved references
                var references = GetResolvedIncludePaths(file);

                // If any match this file...
                if (references.Any(reference => string.Equals(reference, includePath, StringComparison.CurrentCultureIgnoreCase)))
                    // Add this file to be processed
                    toProcess.Add(file);
            });

            // Return what we found
            return toProcess;
        }

        /// <summary>
        /// Returns a list of resolved paths for all include files in a file
        /// </summary>
        /// <param name="filePath">The full path to the file to check</param>
        /// <returns></returns>
        protected List<string> GetResolvedIncludePaths(string filePath)
        {
            // New blank list
            var paths = new List<string>();

            // Make sure the file exists
            if (!FileManager.FileExists(filePath))
                return paths;

            // Read all the file into memory (it's ok we will never have large files they are text web files)
            var fileContents = FileManager.ReadAllText(filePath);

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
                match = Regex.Match(fileContents, mStandard2GroupRegex, RegexOptions.Singleline);

                // Make sure we have enough groups
                if (match.Groups.Count < 3)
                    continue;

                // Make sure this is an include
                if (!string.Equals(match.Groups[1].Value, "include"))
                    continue;

                // Get include path
                var includePath = match.Groups[2].Value;

                // Strip any profile name (we don't care about that for this)
                // If the name have a single : then the right half is the profile name
                if (includePath.Count(c => c == ':') == 1)
                    includePath = includePath.Split(':')[0];

                // Try and find the include file
                FindIncludeFile(filePath, includePath, out string resolvedPath, returnContents: false);

                // Add the resolved path if we got one
                if (!string.IsNullOrEmpty(resolvedPath))
                    paths.Add(resolvedPath);
            }

            // Return the results
            return paths;
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

        #endregion

        #region Dispose

        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {
            // Clean up all file watchers
            mWatchers?.ForEach(watcher =>
            {
                // Get extension
                var extension = watcher.Filter;

                // Dispose of watcher
                watcher.Dispose();

                // Inform listener
                StoppedWatching(extension);

                // Log the message
                Log($"Engine stopped listening for file type {watcher.Filter}");
            });

            if (mWatchers != null)
            {
                // Let listener know we stopped
                Stopped();

                // Log the message
                Log($"Engine stopped");
            }

            mWatchers = null;
        }

        #endregion
    }
}

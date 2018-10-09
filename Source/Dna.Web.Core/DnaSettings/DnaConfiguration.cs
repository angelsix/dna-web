using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using SharpScss;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;

namespace Dna.Web.Core
{
    /// <summary>
    /// The configuration for a DnaWeb environment
    /// </summary>
    /// <remarks>
    /// To add another property:
    /// 
    ///  1. Add public property to DnaConfiguration class, and JsonProperty name to match
    ///  2. Add TryGetSetting for that property into the MergeSettings
    ///  3. Add log output in LogFinalConfiguration
    ///  4. Add any override values from command line interpreters (DnaEnvironment) and such for the new property
    /// 
    /// </remarks>
    public class DnaConfiguration
    {
        #region Public Properties

        /// <summary>
        /// The paths to monitor for files
        /// </summary>
        [JsonProperty(PropertyName = DnaSettings.ConfigurationNameMonitorPath)]
        public string MonitorPath { get; set; }

        /// <summary>
        /// The generate files processing option on start up
        /// </summary>
        [JsonProperty(PropertyName = DnaSettings.ConfigurationNameGenerateOnStart)]
        [JsonConverter(typeof(StringEnumConverter))]
        public GenerateOption? GenerateOnStart { get; set; }

        /// <summary>
        /// True if the engine should spin up, process and then close without waiting for user input
        /// </summary>
        [JsonProperty(PropertyName = DnaSettings.ConfigurationNameProcessAndClose)]
        public bool? ProcessAndClose { get; set; }

        /// <summary>
        /// The level of detail to log out
        /// </summary>
        [JsonProperty(PropertyName = DnaSettings.ConfigurationNameLogLevel)]
        [JsonConverter(typeof(StringEnumConverter))]
        public LogLevel? LogLevel { get; set; }

        /// <summary>
        /// The output path of any output files based on the location of the configuration file
        /// </summary>
        [JsonProperty(PropertyName = DnaSettings.ConfigurationNameOutputPath)]
        public string OutputPath { get; set; }

        /// <summary>
        /// A list of directories that should have a live server spun up for them
        /// </summary>
        [JsonProperty(PropertyName = DnaSettings.ConfigurationNameLiveServerDirectories)]
        public List<string> LiveServerDirectories { get; set; }

        /// <summary>
        /// A list of Live Data Source providers
        /// </summary>
        [JsonProperty(PropertyName = DnaSettings.ConfigurationNameLiveDataSources)]
        public List<DnaConfigurationLiveDataSource> LiveDataSources { get; set; }

        /// <summary>
        /// The cache path where all cache data gets stored
        /// </summary>
        [JsonProperty(PropertyName = DnaSettings.ConfigurationNameCachePath)]
        public string CachePath { get; set; }

        /// <summary>
        /// The output style of the generated css file from a Sass file
        /// </summary>
        [JsonProperty(PropertyName = DnaSettings.ConfigurationNameScssOutputStyle)]
        [JsonConverter(typeof(StringEnumConverter))]
        public ScssOutputStyle? ScssOutputStyle { get; set; }

        /// <summary>
        /// Whether to generate source maps for the css files back to the Scss files to allow debugging in browsers
        /// </summary>
        [JsonProperty(PropertyName = DnaSettings.ConfigurationNameScssGenerateSourceMap)]
        public bool? ScssGenerateSourceMaps { get; set; }

        /// <summary>
        /// If VS Code is detected as installed, on starting up should DnaWeb open the current folder in VS Code
        /// </summary>
        [JsonProperty(PropertyName = DnaSettings.ConfigurationNameOpenVsCode)]
        public bool? OpenVsCode { get; set; }

        [JsonProperty(PropertyName = DnaSettings.ConfigurationNameStaticFolders)]
        public List<DnaConfigurationStaticFolder> StaticFolders { get; set; }

        #endregion

        #region  Public Methods

        /// <summary>
        /// Logs this configuration files settings out as the "Final Configuration" to the log
        /// </summary>
        public void LogFinalConfiguration()
        {
            CoreLogger.Log("Final Configuration", type: LogType.Information);
            CoreLogger.Log("-------------------", type: LogType.Information);
            CoreLogger.LogTabbed("Monitor", MonitorPath, 1, type: LogType.Information);
            CoreLogger.LogTabbed("Generate On Start", GenerateOnStart.ToString(), 1, type: LogType.Information);
            CoreLogger.LogTabbed("Process And Close", ProcessAndClose.ToString(), 1, type: LogType.Information);
            CoreLogger.LogTabbed("Log Level", LogLevel.ToString(), 1, type: LogType.Information);
            CoreLogger.LogTabbed("Output Path", OutputPath, 1, type: LogType.Information);
            CoreLogger.LogTabbed("Scss Output Style", ScssOutputStyle.ToString(), 1, type: LogType.Information);
            CoreLogger.LogTabbed("Scss Generate Source Map", ScssGenerateSourceMaps.ToString(), 1, type: LogType.Information);
            CoreLogger.LogTabbed("Open VS Code", OpenVsCode.ToString(), 1, type: LogType.Information);

            CoreLogger.LogTabbed("Static Folders", (StaticFolders?.Count ?? 0).ToString(), 1, type: LogType.Information);
            if (StaticFolders?.Count > 0)
                StaticFolders.ForEach(directory => CoreLogger.LogTabbed($"{directory.SourceFolder} > {directory.DestinationFolder}", string.Empty, 2, LogType.Information));

            CoreLogger.LogTabbed("Live Servers", (LiveServerDirectories?.Count ?? 0).ToString(), 1, type: LogType.Information);
            if (LiveServerDirectories?.Count > 0)
                LiveServerDirectories.ForEach(directory => CoreLogger.LogTabbed(directory, string.Empty, 2, LogType.Information));

            CoreLogger.LogTabbed("Live Data Sources", (LiveDataSources?.Count ?? 0).ToString(), 1, type: LogType.Information);
            if (LiveDataSources?.Count > 0)
                LiveDataSources.ForEach(source => CoreLogger.LogTabbed(source.ConfigurationFileSource, string.Empty, 2, LogType.Information));

            CoreLogger.Log("", type: LogType.Information);
        }

        #endregion

        #region Public Static Helper Methods

        /// <summary>
        /// Attempts to load a <see cref="DnaConfiguration"/> from a configuration file
        /// </summary>
        /// <param name="filePath">The path to the configuration file</param>
        public static DnaConfiguration LoadFromFile(string filePath)
        {
            try
            {
                // Make sure file exists
                if (!File.Exists(filePath))
                {
                    // Return nothing
                    return null;
                }

                // Json deserialize
                return JsonConvert.DeserializeObject<DnaConfiguration>(File.ReadAllText(filePath));
            }
            catch (Exception ex)
            {
                // Log error
                CoreLogger.Log($"Failed to load configuration file: {filePath}. {ex.Message}", type: LogType.Error);

                return null;
            }
        }

        /// <summary>
        /// Attempts to load a <see cref="DnaConfiguration"/> from a set of configuration files
        /// The order of priority in the list is first is least priority, last is highest.
        /// 
        /// Any values coming after will override the previous values, to create a final
        /// combined <see cref="DnaConfiguration"/> 
        /// </summary>
        /// <param name="filePaths">A list of all paths to the configuration files</param>
        /// <param name="currentConfiguration">The current configuration to merge the settings with</param>
        /// <param name="defaultConfigurationIndex">If specified, it will treat the file path at the index as the default configuration, and so use the environments current directory for relative paths</param>
        /// <param name="currentFolder">The current folder used to resolve relative paths if needed</param>
        /// <param name="globalSettingsOnly">If true, only deal with/merge global settings from the files. For example extracting any LiveServers information</param>
        public static DnaConfiguration LoadFromFiles(string[] filePaths, string currentFolder, DnaConfiguration currentConfiguration = null, int defaultConfigurationIndex = -1, bool globalSettingsOnly = false)
        {
            // Create final setting as default
            var finalSetting = new DnaConfiguration();

            // Copy current settings if they exist
            if (currentConfiguration != null)
                finalSetting = JsonConvert.DeserializeObject<DnaConfiguration>(JsonConvert.SerializeObject(currentConfiguration));

            // For each file
            for (var i = 0; i < filePaths.Length; i++)
            {
                // Get file path
                var filePath = filePaths[i];
                
                // Default configuration uses current directory as relative path source
                var configFolder = i == defaultConfigurationIndex ? Environment.CurrentDirectory : Path.GetDirectoryName(filePath);

                // Try and load the settings
                var settings = LoadFromFile(filePath);

                // TODO: Update to use reflection on properties and set the finalSettings
                //       if the settings properties are not null

                // Make sure we got settings
                if (settings == null)
                    continue;

                // Merge the settings
                MergeSettings(settings, finalSetting, filePath, configFolder, globalSettingsOnly);
            }

            // If output path is not specified, set it to the callers file path
            if (string.IsNullOrEmpty(finalSetting.OutputPath))
                finalSetting.OutputPath = currentFolder;

            // Return the result
            return finalSetting;
        }

        /// <summary>
        /// Get's a default configuration class for the default settings
        /// </summary>
        /// <returns></returns>
        public static DnaConfiguration DefaultConfiguration()
        {
            return new DnaConfiguration
            {
                MonitorPath = ".",
                GenerateOnStart = GenerateOption.All,
                ProcessAndClose = false,
                LogLevel = Core.LogLevel.Informative,
                OutputPath = string.Empty,
                ScssOutputStyle = SharpScss.ScssOutputStyle.Compressed,
                ScssGenerateSourceMaps = false,
                LiveDataSources = new List<DnaConfigurationLiveDataSource>()
                {
                    new DnaConfigurationLiveDataSource
                    {
                       ConfigurationFileSource = "https://raw.githubusercontent.com/angelsix/dna-web-data/master/LiveData/DnaWeb/dna.live.config"
                    },
                    new DnaConfigurationLiveDataSource
                    {
                       ConfigurationFileSource = "https://raw.githubusercontent.com/angelsix/dna-fabric/master/LiveData/Fabric/dna.live.config"
                    },
                },
                LiveServerDirectories = new List<string>(new[] { "../WebRoot" }),
                CachePath = "%LOCALAPPDATA%\\DnaWeb\\%VERSION%\\Cache\\",
            };
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Merges the current settings into the final settings if the values are present (not null or not empty strings)
        /// </summary>
        /// <param name="currentSettings">The settings loaded from the current configuration file</param>
        /// <param name="finalSettings">The final merged configuration settings</param>
        /// <param name="configurationFilePath">The file path to the current configuration settings (for logging purposes)</param>
        /// <param name="currentPath">The current folder that any string path values should be resolved to absolute paths from</param>
        /// <param name="globalSettingsOnly">If true, only deal with/merge global settings from the files. For example extracting any LiveServers information</param>
        private static void MergeSettings(DnaConfiguration currentSettings, DnaConfiguration finalSettings, string configurationFilePath, string currentPath, bool globalSettingsOnly)
        {
            CoreLogger.Log($"{(globalSettingsOnly ? "Global" : "")} Configuration: {configurationFilePath}");

            // If this is not a global load, then load these folder specific settings
            if (!globalSettingsOnly)
            {
                // Monitor Path
                TryGetSetting(() => currentSettings.MonitorPath, () => finalSettings.MonitorPath, resolvePath: true, currentPath: currentPath);

                // Generate On Start
                TryGetSetting(() => currentSettings.GenerateOnStart, () => finalSettings.GenerateOnStart);

                // Process And Close
                TryGetSetting(() => currentSettings.ProcessAndClose, () => finalSettings.ProcessAndClose);

                // Log Level
                TryGetSetting(() => currentSettings.LogLevel, () => finalSettings.LogLevel);

                // Output Path
                TryGetSetting(() => currentSettings.OutputPath, () => finalSettings.OutputPath, resolvePath: true, currentPath: currentPath);

                // Scss Output Style
                TryGetSetting(() => currentSettings.ScssOutputStyle, () => finalSettings.ScssOutputStyle, resolvePath: true, currentPath: currentPath);

                // Scss Generate Source Map 
                TryGetSetting(() => currentSettings.ScssGenerateSourceMaps, () => finalSettings.ScssGenerateSourceMaps, resolvePath: true, currentPath: currentPath);
            }

            // Open Vs Code
            TryGetSetting(() => currentSettings.OpenVsCode, () => finalSettings.OpenVsCode, resolvePath: true, currentPath: currentPath);

            // Static Folders

            // TODO: Improve this and automate it with attributes or something on the properties...
            //       For now manually resolve them
            currentSettings.StaticFolders?.ForEach(staticFolder =>
            {
                staticFolder.SourceFolder = ResolveFullPath(currentPath, staticFolder.SourceFolder, true, out bool wasRelative);
                staticFolder.DestinationFolder = ResolveFullPath(currentPath, staticFolder.DestinationFolder, true, out wasRelative);
            });

            TryGetSettingList(() => currentSettings.StaticFolders, () => finalSettings.StaticFolders, resolvePath: true, currentPath: currentPath,
                logDetails: (item) => $"{item.SourceFolder} > {item.DestinationFolder}");

            // Live Server Directories
            TryGetSettingList(() => currentSettings.LiveServerDirectories, () => finalSettings.LiveServerDirectories, resolvePath: true, currentPath: currentPath);

            // Live Data Sources
            TryGetSettingList(
                () => currentSettings.LiveDataSources,
                () => finalSettings.LiveDataSources, 
                resolvePath: true, 
                currentPath: currentPath,
                logDetails: (item) => item.ConfigurationFileSource
                );

            // Cache Path
            TryGetSetting(() => currentSettings.CachePath, () => finalSettings.CachePath, resolvePath: true, currentPath: currentPath);

            // Space between each configuration details for console log niceness
            CoreLogger.Log("");
        }

        /// <summary>
        /// Get's the setting from the given expression and merges it with the final value expression if it is not null/default
        /// </summary>
        /// <param name="currentValueExpression">The expression to get the current setting property</param>
        /// <param name="finalValueExpression">The expression to get the final setting property</param>
        /// <param name="resolvePath">True if the expression value is a string and the value is a path that should be resolved to absolute if it is relative</param>
        /// <param name="currentPath">The current folder that any string path values should be resolved to absolute paths from</param>
        private static void TryGetSetting<T>(Expression<Func<T>> currentValueExpression, Expression<Func<T>> finalValueExpression, bool resolvePath = false, string currentPath = null)
        {
            // Get the current value
            var currentValue = currentValueExpression.GetPropertyValue();

            // Get property name (for the logs)
            var propertyName = currentValueExpression.GetPropertyName();

            // Get if type is a string
            if (currentValue is string currentString)
            {
                // If string is not null or empty...
                if (!string.IsNullOrEmpty(currentString))
                {
                    // Set final value
                    finalValueExpression.SetPropertyValue<T>(currentValue);

                    // Log it
                    CoreLogger.LogTabbed(propertyName, currentString, 1);

                    if (resolvePath)
                    {
                        // Resolve path
                        var resolvedPath = ResolveFullPath(currentPath ?? string.Empty, currentString, true, out bool wasRelative);

                        // Set resolved path
                        finalValueExpression.SetPropertyValue<T>(resolvedPath);

                        // Log it
                        CoreLogger.LogTabbed($"{propertyName} Resolved", resolvedPath, 1);
                    }
                }
            }
            // Otherwise, if we have a value (not null)...
            else if (currentValue != null)
            {
                // Set final value
                finalValueExpression.SetPropertyValue<T>(currentValue);

                // Log it
                CoreLogger.LogTabbed(propertyName, currentValue.ToString(), 1);
            }
        }

        /// <summary>
        /// Get's the setting from the given expression and merges it with the final value expression if it is not null/default
        /// Used when the item type being provided is a List
        /// </summary>
        /// <param name="currentValueExpression">The expression to get the current setting property</param>
        /// <param name="finalValueExpression">The expression to get the final setting property</param>
        /// <param name="resolvePath">True if the expression value is a string and the value is a path that should be resolved to absolute if it is relative</param>
        /// <param name="currentPath">The current folder that any string path values should be resolved to absolute paths from</param>
        /// <param name="logDetails">If provided, can return the log details for the item passed in</param>
        private static void TryGetSettingList<T>(Expression<Func<List<T>>> currentValueExpression, Expression<Func<List<T>>> finalValueExpression, bool resolvePath = false, string currentPath = null, Func<T, string> logDetails = null)
            where T : class
        {
            // Get the current value
            var currentList = currentValueExpression.GetPropertyValue();

            // Get property name (for the logs)
            var propertyName = currentValueExpression.GetPropertyName();

            // NOTE: We merge lists we don't replace them

            // Get existing list
            var existingList = finalValueExpression.GetPropertyValue() as List<T>;

            // If it's null, create it
            if (existingList == null)
                existingList = new List<T>();

            // Add current values
            if (currentList?.Count > 0)
            {
                currentList.ForEach(currentItem =>
                {
                    // Resolve if this is a string
                    if (currentItem is string)
                        currentItem = ResolveFullPath(currentPath, currentItem as string, true, out bool wasRelative) as T;

                    // Add to list
                    existingList.Add(currentItem);

                    // Log it
                    // Use LogDetails callback if provided
                    // If not check if item is a string
                    // Failing that just log "New Item"
                    var detail = logDetails != null ? logDetails(currentItem) : (currentItem is string ? currentItem as string : "New Item");
                    CoreLogger.LogTabbed(propertyName, detail, 1);
                });

                // Set final setting
                //
                //   NOTE: If list is of classes, and the class implements IEquatible<T>
                //         then this distinct will work for filtering out duplicates
                //
                //         Native values like string lists should automatically match
                //         with hash codes and work
                //
                finalValueExpression.SetPropertyValue<List<T>>(existingList.Distinct().ToList());
            }
        }

        #endregion

        #region Public Helpers

        /// <summary>
        /// Resolves a path to an absolute path, replacing special variables like %LOCALAPPDATA% and %VERSION% (DnaWeb version)
        /// </summary>
        /// <param name="currentDirectory">The current directory to add if the path is relative</param>
        /// <param name="path">The path to resolve to an absolute path</param>
        /// <param name="replaceSpecialVariables">True to replace special variables with their values</param>
        /// <param name="wasRelative">True if this path was relative and has been resolved</param>
        /// <returns></returns>
        public static string ResolveFullPath(string currentDirectory, string path, bool replaceSpecialVariables, out bool wasRelative)
        {
            // Initially relative
            wasRelative = true;

            // Replace any special variables
            if (replaceSpecialVariables)
            {
                // Local App Data folder
                path = path.Replace("%LOCALAPPDATA%", Environment.GetEnvironmentVariable("LocalAppData"));

                // DnaWeb version
                path = path.Replace("%VERSION%", DnaSettings.Version.ToString());
            }

            if (!Path.IsPathRooted(path))
                // Return absolute path
                return Path.GetFullPath(Path.Combine(currentDirectory ?? string.Empty, path));

            // Flag this was not relative
            wasRelative = false;

            // Return path as it's already absolute
            return path;
        }

        #endregion
    }
}
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Dna.Web.Core
{
    /// <summary>
    /// A manager for sourcing and controlling any Live Data
    /// such as Live Variables, Live Templates and more
    /// </summary>
    public class LiveDataManager
    {
        #region Public Properties

        /// <summary>
        /// A list of all sources of LiveData
        /// </summary>
        public List<LiveDataSource> Sources { get; set; }

        /// <summary>
        /// The cache path where Live Data should store its downloads
        /// </summary>
        public string CachePath { get; set; }

        #endregion

        #region Public Methods

        /// <summary>
        /// Downloads and extracts any files from the provided Live Data Sources
        /// </summary>
        /// <param name="sourceConfigurations">The list of sources</param>
        /// <param name="force">Forces installing any download versions regardless of what version exists in the cache</param>
        /// <returns></returns>
        public void DownloadSourcesAsync(List<DnaConfigurationLiveDataSource> sourceConfigurations, bool force = false)
        {
            // Flag if we end up downloading anything
            var somethingDownloaded = false;

            // Log it
            CoreLogger.LogInformation("Updating Live Data Sources...");

            if (sourceConfigurations != null)
            {
                // Keep track of sources we add in this loop
                var addedConfigurations = new List<LiveDataSource>();

                // Loop each source provided...
                foreach (var sourceConfiguration in sourceConfigurations)
                {
                    CoreLogger.Log($"LiveData: Processing source {sourceConfiguration.ConfigurationFileSource}...");

                    var liveDataSource = new LiveDataSource();

                    #region Get Source Configuration 

                    // Is source a web link?
                    var isWeb = sourceConfiguration.ConfigurationFileSource.ToLower().StartsWith("http");

                    // Is source a local configuration file
                    var isLocal = sourceConfiguration.ConfigurationFileSource.ToLower().EndsWith(DnaSettings.LiveDataConfigurationFileName.ToLower());

                    // Is source folder? (used directly, not downloaded to cache folder)
                    // Detected by being a folder link that inside that folder exists a dna.live.config file
                    var isDirectSource = !isWeb && !isLocal && File.Exists(Path.Combine(sourceConfiguration.ConfigurationFileSource, DnaSettings.LiveDataConfigurationFileName));

                    // If this is a web source...
                    if (isWeb)
                    {
                        #region Download Configuration File

                        // Download its information
                        var informationString = WebHelpers.DownloadString(sourceConfiguration.ConfigurationFileSource);

                        // If it is null, it failed
                        if (string.IsNullOrEmpty(informationString))
                        {
                            // Log it
                            CoreLogger.Log($"LiveData: Failed to download configuration {sourceConfiguration.ConfigurationFileSource}", type: LogType.Warning);

                            // Stop
                            continue;
                        }

                        #endregion

                        #region Deserialize

                        // Try to deserialize the Json
                        try
                        {
                            liveDataSource = JsonConvert.DeserializeObject<LiveDataSource>(informationString);
                        }
                        catch (Exception ex)
                        {
                            // If we failed, log it
                            CoreLogger.Log($"LiveData: Failed to deserialize configuration {sourceConfiguration.ConfigurationFileSource}. {ex.Message}", type: LogType.Warning);

                            // Stop
                            continue;
                        }

                        #endregion
                    }
                    // If it ends with dna.live.config and the local file exists
                    else if (isLocal)
                    {
                        if (!File.Exists(sourceConfiguration.ConfigurationFileSource))
                        {
                            // Log it
                            CoreLogger.Log($"LiveData: Local configuration file not found {sourceConfiguration.ConfigurationFileSource}", type: LogType.Warning);

                            // Stop
                            continue;
                        }

                        #region Read Configuration File

                        // Read its information
                        var informationString = File.ReadAllText(sourceConfiguration.ConfigurationFileSource);

                        // If it is null, it failed
                        if (string.IsNullOrEmpty(informationString))
                        {
                            // Log it
                            CoreLogger.Log($"LiveData: Failed to read local configuration {sourceConfiguration.ConfigurationFileSource}", type: LogType.Warning);

                            // Stop
                            continue;
                        }

                        #endregion

                        #region Deserialize

                        // Try to deserialize the Json
                        try
                        {
                            liveDataSource = JsonConvert.DeserializeObject<LiveDataSource>(informationString);
                        }
                        catch (Exception ex)
                        {
                            // If we failed, log it
                            CoreLogger.Log($"LiveData: Failed to deserialize configuration {sourceConfiguration.ConfigurationFileSource}. {ex.Message}", type: LogType.Warning);

                            // Stop
                            continue;
                        }

                        #endregion
                    }
                    // Otherwise...
                    else
                    {
                        // If it is a folder that exists and contains the dna.live.config file
                        // specifying it this way means it should be treated as a direct access
                        // local file (not copied to the cache folder)
                        //
                        // So, ignore it for this step either way but if it doesn't contain 
                        // a configuration file, warn it is an unknown source
                        if (!isDirectSource)
                            // Log it
                            CoreLogger.Log($"LiveData: Unknown source type {sourceConfiguration.ConfigurationFileSource}", type: LogType.Warning);
                        else
                            // Log it
                            CoreLogger.Log($"LiveData: Skipping local source folder (will be used directly not copied to cache) {sourceConfiguration.ConfigurationFileSource}");

                        // Stop either way
                        continue;
                    }

                    #endregion

                    #region Newer Version Check

                    // If we are forcing an update ignore this step
                    if (!force)
                    {
                        // Check if we have a newer version...
                        var newerVersion = Sources.FirstOrDefault(localSource =>
                            // Has the same name...
                            localSource.Name.EqualsIgnoreCase(liveDataSource.Name) &&
                            // And a higher version
                            localSource.Version >= liveDataSource.Version);

                        if (newerVersion != null)
                        {
                            // Log it
                            CoreLogger.Log($"LiveData: Skipping download as same or newer version exists {newerVersion.Name} ({newerVersion.CachedFilePath})");

                            // Stop
                            continue;
                        }
                    }

                    #endregion

                    #region Delete Old Versions

                    // Find any older version and delete it
                    var olderVersions = Sources.Where(localSource =>
                        // Has the same name...
                        localSource.Name.EqualsIgnoreCase(liveDataSource.Name) &&
                        // And a lower version (or we are forcing an update)
                        (force || localSource.Version < liveDataSource.Version)).ToList();

                    // If we got any lower versions...
                    if (olderVersions?.Count > 0)
                    {
                        // Loop each older version...
                        foreach (var olderVersion in olderVersions)
                        {
                            try
                            {
                                // Try and delete the folder
                                Directory.Delete(olderVersion.CachedFilePath, true);
                            }
                            catch (Exception ex)
                            {
                                // Log it
                                CoreLogger.Log($"LiveData: Failed to delete older version {olderVersion.CachedFilePath}. {ex.Message}", type: LogType.Warning);

                                // Stop
                                continue;
                            }
                        }
                    }

                    #endregion

                    #region Download Source

                    var zipFile = isWeb ?
                        // If Web: New unique filename to download to
                        FileHelpers.GetUnusedPath(Path.Combine(CachePath, $"{liveDataSource.Name}.zip")) :
                        // Otherwise source should point to zip file relative to current path
                        DnaConfiguration.ResolveFullPath(Path.GetDirectoryName(sourceConfiguration.ConfigurationFileSource), liveDataSource.Source, true, out bool wasRelative);

                    if (isWeb)
                    {
                        // Now attempt to download the source zip file
                        CoreLogger.Log($"LiveData: Downloading source contents... {liveDataSource.Source}");

                        // Download to folder
                        var downloaded = WebHelpers.DownloadFile(liveDataSource.Source, zipFile);

                        // If it failed to download...
                        if (!downloaded)
                        {
                            // Log it
                            CoreLogger.Log($"LiveData: Failed to download source file {liveDataSource.Source}", type: LogType.Warning);

                            // Stop
                            continue;
                        }
                    }
                    else
                    {
                        // Make sure zip exists
                        if (!File.Exists(zipFile))
                        {
                            // Log it
                            CoreLogger.Log($"LiveData: Local source zip file does not exist {zipFile}", type: LogType.Warning);

                            // Stop
                            continue;
                        }
                    }

                    // Get unused folder to extract to
                    var saveFolder = FileHelpers.GetUnusedPath(Path.Combine(CachePath, liveDataSource.Name));

                    #endregion

                    // Flag if we succeeded so the local sources get refreshed after we are done
                    somethingDownloaded = true;

                    // Whatever happens now, fail or succeed, we should clean up the downloaded zip
                    try
                    {
                        #region Extract Source

                        // Try and extract the zip
                        var unzipSuccessful = ZipHelpers.Unzip(zipFile, saveFolder);

                        if (!unzipSuccessful)
                        {
                            // Log it
                            CoreLogger.Log($"LiveData: Failed to unzip downloaded file {zipFile}", type: LogType.Warning);

                            // Clean up folder
                            try
                            {
                                // If save folder exists...
                                if (Directory.Exists(saveFolder))
                                    // Delete it
                                    Directory.Delete(saveFolder, true);
                            }
                            catch (Exception ex)
                            {
                                // Log it
                                CoreLogger.Log($"LiveData: Failed to delete failed extraction folder {saveFolder}. {ex.Message}", type: LogType.Warning);
                            }

                            // Stop
                            continue;
                        }

                        #endregion

                        #region Verify Valid Configuration

                        // Verify the zip has valid dna.live.config file in and it successfully parses

                        // Get expected configuration path
                        var configFilePath = Path.Combine(saveFolder, DnaSettings.LiveDataConfigurationFileName);

                        // Flag if it is a valid source
                        var validSource = true;

                        // If the file does not exist or it fails to parse
                        if (!File.Exists(configFilePath))
                        {
                            // Log it
                            CoreLogger.Log($"LiveData: Live Data configuration file missing {configFilePath}.", type: LogType.Warning);

                            // Flag it
                            validSource = false;
                        }
                        else
                        {
                            // Try and parse the file
                            try
                            {
                                // Try and parse
                                var result = JsonConvert.DeserializeObject<LiveDataSource>(File.ReadAllText(configFilePath));

                                #region Already Added Check

                                // Make sure we don't already have this name
                                if (addedConfigurations.Any(source => source.Name.EqualsIgnoreCase(result.Name)))
                                {
                                    // Log it
                                    CoreLogger.Log($"LiveData: Ignoring source as another exists with same name {result.Name}. {result.CachedFilePath}", type: LogType.Warning);

                                    // Flag it
                                    validSource = false;
                                }

                                #endregion

                                // If it is a valid source...
                                if (validSource)
                                {
                                    // Add to already added list
                                    addedConfigurations.Add(result);

                                    // Log successful install
                                    CoreLogger.Log($"Installed new Live Data Source {result.Name} v{result.Version}, from {sourceConfiguration.ConfigurationFileSource}", type: LogType.Success);
                                }
                            }
                            catch (Exception ex)
                            {
                                // Log it
                                CoreLogger.Log($"LiveData: Failed to parse Live Data configuration file {configFilePath}. {ex.Message}", type: LogType.Error);

                                // Flag it
                                validSource = false;
                            }
                        }

                        // If it is not a valid file...
                        if (!validSource)
                        {
                            // Log it
                            CoreLogger.Log($"LiveData: Cleaning invalid source folder {saveFolder}.", type: LogType.Warning);
                            
                            // Delete source folder
                            DeleteSource(saveFolder);
                        }

                        #endregion
                    }
                    finally
                    {
                        // If it was a downloaded file...
                        if (isWeb)
                        {
                            // Log it
                            CoreLogger.Log($"LiveData: Cleaning up downloaded file {zipFile}");

                            try
                            {
                                // Try and delete it
                                File.Delete(zipFile);
                            }
                            catch (Exception ex)
                            {
                                // Log it
                                CoreLogger.Log($"LiveData: Failed to delete downloaded file {zipFile}. {ex.Message}", type: LogType.Error);
                            }
                        }
                    }
                }
            }

            // Rescan if we downloaded anything
            if (somethingDownloaded)
                // Refresh local sources
                RefreshLocalSources(sourceConfigurations);

            CoreLogger.Log($"LiveData: Finished downloading sources");
        }

        /// <summary>
        /// Refreshes the local sources information, rescanning the cache folder 
        /// and updating the <see cref="Sources"/>
        /// </summary>
        /// <param name="sourceConfigurations">The list of sources</param>
        /// <param name="log">True if any log output should be output</param>
        public void RefreshLocalSources(List<DnaConfigurationLiveDataSource> sourceConfigurations, bool log = true)
        {
            // Log it
            if (log)
                CoreLogger.Log("LiveData: Refreshing local sources...");

            // Empty list
            Sources = new List<LiveDataSource>();

            // Scan the cache folder for all dna.live.config files
            var allConfigs = FileHelpers.GetDirectoryFiles(CachePath, DnaSettings.LiveDataConfigurationFileName).ToList();

            // Add any local source configurations that are direct sources
            var directLocalSources = sourceConfigurations.Where(source =>
            {
                // Is source folder? (used directly, not downloaded to cache folder)
                // Detected by being a folder link that inside that folder exists a dna.live.config file
                // Is direct folder link if...
                var isDirectSource = 
                    // It isn't a web link
                    !(source.ConfigurationFileSource.ToLower().StartsWith("http")) &&
                    // It isn't a direct link to a configuration file 
                    !(source.ConfigurationFileSource.ToLower().EndsWith(DnaSettings.LiveDataConfigurationFileName.ToLower())) &&
                    // And it contains a configuration file inside it's folder
                    File.Exists(Path.Combine(source.ConfigurationFileSource, DnaSettings.LiveDataConfigurationFileName));

                // Return if it is
                return isDirectSource;
            }).Select(source => Path.Combine(source.ConfigurationFileSource, DnaSettings.LiveDataConfigurationFileName)).ToList();

            // Add any found direct local sources
            if (directLocalSources?.Count > 0)
                allConfigs.AddRange(directLocalSources);

            // Loop all configuration sources
            allConfigs.ForEach(config =>
            {
                // Get the directory of this configuration file
                var configDirectory = Path.GetDirectoryName(config);

                // Check if it is in this folder
                var isCached = configDirectory.StartsWith(CachePath);

                try
                {
                    #region Parse Configuration

                    // Parse the configuration file
                    var liveDataSource = JsonConvert.DeserializeObject<LiveDataSource>(File.ReadAllText(config));

                    // Set the local cache folder
                    liveDataSource.CachedFilePath = configDirectory;

                    #endregion

                    #region Scan Variables

                    // Scan folder for variable files
                    var variableFolder = Path.Combine(configDirectory, DnaSettings.LiveDataFolderVariables);

                    // Get all files ending with 
                    var variablePaths = FileHelpers.GetDirectoryFiles(variableFolder, $"*{DnaSettings.LiveDataFolderVariablesExtension}").ToList();

                    // Empty variables list 
                    liveDataSource.Variables = new List<LiveDataSourceVariable>();

                    // Add each item, ignoring duplicates
                    variablePaths.ForEach(variablePath =>
                    {
                        // Get variable name from filename
                        var variableName = Path.GetFileNameWithoutExtension(variablePath);

                        // Name cannot have a . in it (we use it to separate prefix)
                        if (variableName.Contains('.'))
                        {
                            // Log it
                            if (log)
                                CoreLogger.Log($"LiveData: Variable cannot have . in the name {config}.", type: LogType.Error);

                            // Delete source if cached
                            if (isCached)
                                DeleteSource(liveDataSource.CachedFilePath, log: log);
                        }

                        // See if we have another variable with same name
                        var existingVariable = liveDataSource.Variables.FirstOrDefault(f => f.Name.EqualsIgnoreCase(variableName));

                        // If one already exists...
                        if (existingVariable != null)
                        {
                            // Log it
                            if (log)
                                CoreLogger.Log($"LiveData: Skipping variable {variablePath}, same name already exists {existingVariable.FilePath}", type: LogType.Warning);
                        }
                        // Otherwise...
                        else
                        {
                            // Add it
                            liveDataSource.Variables.Add(new LiveDataSourceVariable
                            {
                                FilePath = variablePath,
                                Name = variableName
                            });
                        }
                    });

                    #endregion

                    #region Scan Templates

                    // Scan folder for variable files
                    var templateFolder = Path.Combine(configDirectory, DnaSettings.LiveDataFolderTemplates);

                    // Get all files ending with template extension
                    var templatePaths = FileHelpers.GetDirectoryFiles(templateFolder, $"*{DnaSettings.LiveDataFolderTemplateExtension}").ToList();

                    // Empty templates list 
                    liveDataSource.Templates = new List<LiveDataSourceTemplate>();

                    // Add each item, ignoring duplicates
                    templatePaths.ForEach(templatePath =>
                    {
                        // Get template name from filename
                        var templateName = Path.GetFileNameWithoutExtension(templatePath);

                        // Name cannot have a . in it (we use it to separate prefix)
                        if (templateName.Contains('.'))
                        {
                            // Log it
                            if (log)
                                CoreLogger.Log($"LiveData: Template cannot have . in the name {config}.", type: LogType.Error);

                            // Delete source if cached
                            if (isCached)
                                DeleteSource(liveDataSource.CachedFilePath, log: log);
                        }

                        // See if we have another template with same name
                        var existingTemplate = liveDataSource.Templates.FirstOrDefault(f => f.Name.EqualsIgnoreCase(templateName));

                        // If one already exists...
                        if (existingTemplate != null)
                        {
                            // Log it
                            if (log)
                                CoreLogger.Log($"LiveData: Skipping template {templatePath}, same name already exists {existingTemplate.FilePath}", type: LogType.Warning);
                        }
                        // Otherwise...
                        else
                        {
                            // Add it
                            liveDataSource.Templates.Add(new LiveDataSourceTemplate
                            {
                                FilePath = templatePath,
                                Name = templateName
                            });
                        }
                    });

                    #endregion

                    #region Add Source

                    // Make sure we don't already have this name
                    if (Sources.Any(source => source.Name.EqualsIgnoreCase(liveDataSource.Name)))
                    {
                        // Log it
                        if (log)
                            CoreLogger.Log($"LiveData: Invalid source as another exists with same name {liveDataSource.Name}. {liveDataSource.CachedFilePath}", type: LogType.Warning);

                        // Delete source if this is in the cache folder
                        if (isCached)
                            DeleteSource(liveDataSource.CachedFilePath, log: log);

                        // Done
                        return;
                    }

                    // Add this to the Sources
                    Sources.Add(liveDataSource);

                    // Log it
                    if (log)
                        CoreLogger.Log($"Live Data Source {liveDataSource.Name} v{liveDataSource.Version}", type: LogType.Information);

                    liveDataSource.Log(LogType.Diagnostic);

                    #endregion
                }
                catch (Exception ex)
                {
                    #region Clean Up on Failure

                    if (log)
                    {
                        // Log it
                        CoreLogger.Log($"LiveData: Failed to parse cached source {config}. {ex.Message}", type: LogType.Error);

                        // Delete folder as it's useless
                        CoreLogger.Log($"LiveData: Deleting corrupt cache folder {configDirectory}", type: LogType.Warning);
                    }

                    try
                    {
                        // Delete source cache folder
                        Directory.Delete(configDirectory, true);
                    }
                    catch (Exception innerEx)
                    {
                        // Log it
                        if (log)
                            CoreLogger.Log($"LiveData: Failed to clean up corrupt cached source {configDirectory}. {innerEx.Message}", type: LogType.Error);
                    }

                    #endregion
                }
            });
        }

        /// <summary>
        /// Removes a cached Live Data Source by file path
        /// </summary>
        /// <param name="cachedFilePath"></param>
        /// <param name="configurationSources">A list of sources to use when refreshing the local sources</param>
        /// <param name="refreshLocalSources">True it the local source list should be refreshed</param>
        /// <param name="log">True if log output should be generated</param>
        public void DeleteSource(string cachedFilePath, List<DnaConfigurationLiveDataSource> configurationSources = null, bool refreshLocalSources = false, bool log = true)
        {
            // Delete the source folder
            try
            {
                // If folder exists...
                if (Directory.Exists(cachedFilePath))
                {
                    // Delete it
                    Directory.Delete(cachedFilePath, true);

                    // Log it
                    if (log)
                        CoreLogger.Log($"LiveData: Deleted cached Live Data Source folder {cachedFilePath}.", type: LogType.Warning);

                    // If we should refresh local sources
                    if (refreshLocalSources)
                        // Refresh local cache
                        RefreshLocalSources(configurationSources);
                }
                else
                {
                    // Log it
                    if (log)
                        CoreLogger.Log($"LiveData: Live Data Source cache folder not found {cachedFilePath}.", type: LogType.Warning);
                }
            }
            catch (Exception ex)
            {
                // Log it
                if (log)
                    CoreLogger.Log($"LiveData: Failed to delete cached Live Data Source folder {cachedFilePath}. {ex.Message}", type: LogType.Error);
            }
        }

        /// <summary>
        /// Removes all cache data (the entire Live Data cache folder
        /// </summary>
        /// <param name="configurationSources">A list of sources to use when refreshing the local sources</param>
        public void DeleteAllSources(List<DnaConfigurationLiveDataSource> configurationSources)
        {
            // Delete the root folder
            DeleteSource(CachePath, configurationSources, refreshLocalSources: true);
        }

        /// <summary>
        /// Find's a variable by its name (with prefix) and returns it if found
        /// </summary>
        /// <param name="name">The name of the variable</param>
        /// <returns></returns>
        public LiveDataSourceVariable FindVariable(string name)
        {
            // If there is no dot in the name...
            if (!name.Contains('.'))
                // Add default prefix
                name = $"{DnaSettings.LiveDataDefaultPrefix}.{name}";

            // Get a variable that has the matching prefix.name
            var foundVariable = Sources?.Where(source => source.Variables.Any(variable => $"{source.Prefix}.{variable.Name}".EqualsIgnoreCase(name)))
                                        .Select(source => source.Variables.First(variable => $"{source.Prefix}.{variable.Name}".EqualsIgnoreCase(name)))
                                        .FirstOrDefault();

            // Return result
            return foundVariable;
        }

        /// <summary>
        /// Find's a template by its name (with prefix) and returns it if found
        /// </summary>
        /// <param name="name">The name of the template</param>
        /// <returns></returns>
        public LiveDataSourceTemplate FindTemplate(string name)
        {
            // If there is no dot in the name...
            if (!name.Contains('.'))
                // Add default prefix
                name = $"{DnaSettings.LiveDataDefaultPrefix}.{name}";

            // Get a variable that has the matching prefix.name
            var foundTemplate = Sources?.Where(source => source.Templates.Any(template => $"{source.Prefix}.{template.Name}".EqualsIgnoreCase(name)))
                                        .Select(source => source.Templates.First(template => $"{source.Prefix}.{template.Name}".EqualsIgnoreCase(name)))
                                        .FirstOrDefault();

            // Return result
            return foundTemplate;
        }

        /// <summary>
        /// Find's a Source by its name and returns it if found
        /// </summary>
        /// <param name="name">The name of the source</param>
        /// <returns></returns>
        public LiveDataSource FindSource(string name)
        {
            return Sources?.FirstOrDefault(source => source.Name.EqualsIgnoreCase(name));
        }

        #endregion

        #region Logging

        /// <summary>
        /// Writes an overview of all sources to the log
        /// </summary>
        /// <param name="logLevel">The log level</param>
        public void LogAllSources(LogType logLevel = LogType.Information)
        {
            // If we have none...
            if (Sources?.Count == 0)
                // Log it
                CoreLogger.Log("No Live Data Sources. Available sources are: ", type: logLevel);

            // Write a line per source
            // Showing the name and version
            Sources.ForEach(source => CoreLogger.LogTabbed($"{source.Name} v{source.Version}", string.Empty, 1, type: logLevel));
        }

        /// <summary>
        /// Writes an overview of all variables to the log
        /// </summary>
        /// <param name="logLevel">The log level</param>
        public void LogAllVariables(LogType logLevel = LogType.Information)
        {
            // If we have none...
            if (Sources?.Count == 0)
                // Log it
                CoreLogger.Log("No Live Data Variables", type: logLevel);

            // Log all variables
            Sources.ForEach(source => source.Variables?.ForEach(variable => CoreLogger.LogTabbed($"{source.Prefix}.{variable.Name}", string.Empty, 1, logLevel)));
        }

        /// <summary>
        /// Writes an overview of all templates to the log
        /// </summary>
        /// <param name="logLevel">The log level</param>
        public void LogAllTemplates(LogType logLevel = LogType.Information)
        {
            // If we have none...
            if (Sources?.Count == 0)
                // Log it
                CoreLogger.Log("No Live Data Templates", type: logLevel);

            // Log all templates
            Sources.ForEach(source => source.Templates?.ForEach(variable => CoreLogger.LogTabbed($"{source.Prefix}.{variable.Name}", string.Empty, 1, logLevel)));
        }

        /// <summary>
        /// Writes an detail summary of a specific source
        /// </summary>
        /// <param name="name">The name of the source</param>
        /// <param name="logLevel">The log level</param>
        public void LogSourceDetails(string name, LogType logLevel = LogType.Information)
        {
            // Try and find a source by that name
            var foundSource = Sources?.FirstOrDefault(source => source.Name.EqualsIgnoreCase(name));

            // If we didn't find one...
            if (foundSource == null)
            {
                // Log it
                CoreLogger.Log($"Live Data Source '{name}' not found", type: logLevel);

                // Show available sources
                LogAllSources(logLevel);

                return;
            }

            // Log source details
            foundSource.Log(logLevel);
        }

        #endregion
    }
}

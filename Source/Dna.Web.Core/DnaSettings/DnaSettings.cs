using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Dna.Web.Core
{
    /// <summary>
    /// Global settings for the DnaWeb system
    /// </summary>
    public static class DnaSettings
    {
        #region Public Static Members

        /// <summary>
        /// The filename of the configuration file
        /// </summary>
        public static string ConfigurationFileName => "dna.config";

        /// <summary>
        /// The filename of the Live Data Sources configuration file
        /// </summary>
        public static string LiveDataConfigurationFileName => "dna.live.config";

        /// <summary>
        /// The extension used for DnaWeb files that generate HTML content
        /// </summary>
        public static string DnaWebFileExtension => ".dhtml";

        /// <summary>
        /// The folder name where Live Data Variables are stored
        /// </summary>
        public static string LiveDataFolderVariables => "Variables";

        /// <summary>
        /// The file extension for Live Data Variable files
        /// </summary>
        public static string LiveDataFolderVariablesExtension => ".dna";

        /// <summary>
        /// The folder name where Live Data Templates are stored
        /// </summary>
        public static string LiveDataFolderTemplates => "Templates";

        /// <summary>
        /// The file extension for Live Data Template files
        /// </summary>
        public static string LiveDataFolderTemplateExtension => ".zip";

        /// <summary>
        /// The default prefix to use when finding variables/templates if one is not specified
        /// </summary>
        public static string LiveDataDefaultPrefix => "dna";

        /// <summary>
        /// The location of the configuration file to load based on the current environment 
        /// (usually loading a configuration file from the location we started up from)
        /// </summary>
        public static string SpecificConfigurationFilePath => Path.Combine(Environment.CurrentDirectory, ConfigurationFileName);

        /// <summary>
        /// The location of the default configuration file to load before any other configuration file
        /// </summary>
        public static string DefaultConfigurationFilePath => Path.Combine(AppContext.BaseDirectory, ConfigurationFileName);

        /// <summary>
        /// The version of DnaWeb
        /// </summary>
        public static Version Version { get; private set; } = typeof(DnaSettings).Assembly.GetName().Version;

        /// <summary>
        /// The platform of DnaWeb (such as Windows 32bit, Mac OSX, Linux 64bit)
        /// </summary>
        public static Platform Platform { get; private set; } = GetPlatform();

        #endregion

        #region Public Constant Members

        #region General

        /// <summary>
        /// The folder name where Live Data Sources are stored in the cache folder
        /// </summary>
        public const string CacheSubFolderLiveData = "LiveData";

        #endregion

        #region dna.config Json Names

        /// <summary>
        /// The name of the configuration setting for the Monitor Path
        /// </summary>
        public const string ConfigurationNameMonitorPath = "monitor";

        /// <summary>
        /// The name of the configuration setting for the Generate On Start
        /// </summary>
        public const string ConfigurationNameGenerateOnStart = "generateOnStart";

        /// <summary>
        /// The name of the configuration setting for the Process And Close
        /// </summary>
        public const string ConfigurationNameProcessAndClose = "processAndClose";

        /// <summary>
        /// The name of the configuration setting for the Log Level
        /// </summary>
        public const string ConfigurationNameLogLevel = "logLevel";

        /// <summary>
        /// The name of the configuration setting for the Output Path
        /// </summary>
        public const string ConfigurationNameOutputPath = "outputPath";

        /// <summary>
        /// The name of the configuration setting for the Live Server Directories
        /// </summary>
        public const string ConfigurationNameLiveServerDirectories = "liveServers";

        /// <summary>
        /// The name of the configuration setting for the Live Data Sources
        /// </summary>
        public const string ConfigurationNameLiveDataSources = "liveDataSources";

        /// <summary>
        /// The name of the configuration setting for a Live Data Source class Source property
        /// </summary>
        public const string ConfigurationNameLiveDataSource = "source";

        /// <summary>
        /// The name of the configuration setting for Cache Path
        /// </summary>
        public const string ConfigurationNameCachePath = "cachePath";

        /// <summary>
        /// The name of the configuration setting for Scss Output Style
        /// </summary>
        public const string ConfigurationNameScssOutputStyle = "scssOutputStyle";

        /// <summary>
        /// The name of the configuration setting for Scss Generate Source Map
        /// </summary>
        public const string ConfigurationNameScssGenerateSourceMap = "scssGenerateSourceMap";

        /// <summary>
        /// The name of the configuration setting for Open Vs Code
        /// </summary>
        public const string ConfigurationNameOpenVsCode = "openVsCode";

        /// <summary>
        /// The name of the configuration setting for Static Folders
        /// </summary>
        public const string ConfigurationNameStaticFolders = "staticFolders";

        /// <summary>
        /// The name of the configuration setting for Static Folders Source property
        /// </summary>
        public const string ConfigurationNameStaticFolderSource = "source";

        /// <summary>
        /// The name of the configuration setting for Static Folders Destination property
        /// </summary>
        public const string ConfigurationNameStaticFolderDestination = "destination";

        #endregion

        #endregion

        #region Private Helpers

        /// <summary>
        /// Gets the platform of this machine (windows/linux/mac, 32/64bit)
        /// </summary>
        /// <returns></returns>
        private static Platform GetPlatform()
        {
            // If windows...
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                // Return Windows 32/64bit
                return RuntimeInformation.OSArchitecture == Architecture.X64 ? Platform.Windows64 : Platform.Windows32;
            // If linux...
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                // Return Linux 32/64bit
                return RuntimeInformation.OSArchitecture == Architecture.X64 ? Platform.Linux64 : Platform.Linux32;
            // If osx...
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                // Return OSX
                return Platform.Osx;

            // Anything else is unknown
            // Log it
            CoreLogger.Log("Running on unknown architecture!", type: LogType.Error);

            // Break to debugger
            Debugger.Break();

            // Return unknown
            return Platform.Unknown;
        }

        #endregion
    }
}

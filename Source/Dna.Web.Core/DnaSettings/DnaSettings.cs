using System;
using System.IO;

namespace Dna.Web.Core
{
    /// <summary>
    /// Global settings for the Dna Web system
    /// </summary>
    public static class DnaSettings
    {
        /// <summary>
        /// The filename of the configuration file
        /// </summary>
        public static string ConfigurationFileName => "dna.config";

        /// <summary>
        /// The location of the configuration file to load based on the current environment 
        /// (usually loading a configuration file from the location we we started up from)
        /// </summary>
        public static string SpecificConfigurationFilePath => Path.Combine(Environment.CurrentDirectory, ConfigurationFileName);

        /// <summary>
        /// The location of the default configuration file to load before any other configuration file
        /// </summary>
        public static string DefaultConfigurationFilePath => Path.Combine(AppContext.BaseDirectory, ConfigurationFileName);

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
    }
}

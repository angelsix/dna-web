using System;
using System.Collections.Generic;

namespace Dna.Web.Core
{
    /// <summary>
    /// A source for DNA Live Data such as a GitHub repository
    /// or a local folder
    /// </summary>
    public class LiveDataSource
    {
        #region Public Properties

        /// <summary>
        /// The version of this source
        /// </summary>
        public Version Version { get; set; }

        /// <summary>
        /// The author of this source
        /// </summary>
        public string Author { get; set; }

        /// <summary>
        /// The short name that this source is known by
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// A more detailed description of this source, and what it provides
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// The prefix used to access the data source in commands
        /// </summary>
        public string Prefix { get; set; }

        /// <summary>
        /// The path/url of the Live Data source zip file
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// For locally downloaded sources, the directory of this source on disk
        /// </summary>
        public string CachedFilePath { get; set; }

        /// <summary>
        /// All Live Data Variables found in this source
        /// </summary>
        public List<LiveDataSourceVariable> Variables { get; set; }

        /// <summary>
        /// All Live Data Templates found in this source
        /// </summary>
        public List<LiveDataSourceTemplate> Templates { get; set; }

        /// <summary>
        /// Log details about this source to the log
        /// </summary>
        /// <param name="logLevel">The log level</param>
        public void Log(LogType logLevel)
        {
            CoreLogger.LogTabbed("Name", Name, 1, type: logLevel);
            CoreLogger.LogTabbed("Description", Description, 1, type: logLevel);
            CoreLogger.LogTabbed("Author", Author, 1, type: logLevel);
            CoreLogger.LogTabbed("Prefix", Prefix, 1, type: logLevel);
            CoreLogger.LogTabbed("Cache Path", CachedFilePath, 1, type: logLevel);
            CoreLogger.LogTabbed("Source", Source, 1, type: logLevel);
            CoreLogger.LogTabbed("Variables", Variables.Count.ToString(), 1, type: logLevel);
            Variables.ForEach(variable => CoreLogger.LogTabbed(variable.Name, string.Empty, 2, type: logLevel));
            CoreLogger.LogTabbed("Templates", Templates.Count.ToString(), 1, type: logLevel);
            Templates.ForEach(template => CoreLogger.LogTabbed(template.Name, string.Empty, 2, type: logLevel));
        }

        #endregion
    }
}

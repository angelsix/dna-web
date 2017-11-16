using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Text;

namespace Dna.Web.Core
{
    public class AutoUpdateReleases
    {
        /// <summary>
        /// A list of all releases being made available to the auto-updater
        /// </summary>
        public List<AutoUpdateRelease> Releases { get; set; }
    }

    /// <summary>
    /// A class representing a DnaWeb release information
    /// </summary>
    public class AutoUpdateRelease
    {
        /// <summary>
        /// The version number
        /// </summary>
        public Version Version { get; set; }

        /// <summary>
        /// The platform for this installer
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public Platform? Platform { get; set; }

        /// <summary>
        /// The download path to the installer
        /// </summary>
        public string Path { get; set; }
    }
}

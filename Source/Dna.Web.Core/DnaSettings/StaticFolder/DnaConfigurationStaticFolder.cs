using Newtonsoft.Json;
using System;

namespace Dna.Web.Core
{
    /// <summary>
    /// Details about a folder that should be processed as a static folder 
    /// having its source copied to an output folder on any change
    /// </summary>
    public class DnaConfigurationStaticFolder : IEquatable<DnaConfigurationStaticFolder>
    {
        #region Public Properties

        /// <summary>
        /// The source folder
        /// </summary>
        [JsonProperty(PropertyName = DnaSettings.ConfigurationNameStaticFolderSource)]
        public string SourceFolder { get; set; }

        /// <summary>
        /// The destination folder
        /// </summary>
        [JsonProperty(PropertyName = DnaSettings.ConfigurationNameStaticFolderDestination)]
        public string DestinationFolder { get; set; }

        #endregion

        #region Equality

        /// <summary>
        /// Checks if this and the provided <see cref="DnaConfigurationStaticFolder"/> are the same
        /// based on if they have the same source and destination
        /// </summary>
        /// <param name="other">The other item</param>
        /// <returns></returns>
        public bool Equals(DnaConfigurationStaticFolder other)
        {
            // Return if the details are the same
            return SourceFolder.EqualsIgnoreCase(other.SourceFolder) ||
                   DestinationFolder.EqualsIgnoreCase(other.DestinationFolder);
        }

        /// <summary>
        /// Gets the hash code for this item
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            // Return the hash code of the combined string
            return $"{SourceFolder}>{DestinationFolder}".GetHashCode();
        }

        #endregion
    }
}

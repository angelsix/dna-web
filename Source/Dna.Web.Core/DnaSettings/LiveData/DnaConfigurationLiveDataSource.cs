using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Dna.Web.Core
{
    /// <summary>
    /// A configuration for a DnaWeb environment, specifically a Live Data Source
    /// </summary>
    public class DnaConfigurationLiveDataSource : IEquatable<DnaConfigurationLiveDataSource>
    {
        #region Public Properties

        /// <summary>
        /// The path/url to the source dna.live.config file
        /// </summary>
        [JsonProperty(PropertyName = DnaSettings.ConfigurationNameLiveDataSource)]
        public string ConfigurationFileSource { get; set; }

        #endregion

        #region Equality

        /// <summary>
        /// Checks if this and the provided <see cref="DnaConfigurationLiveDataSource"/> are the same
        /// based on if they point to the same source
        /// </summary>
        /// <param name="other">The other item</param>
        /// <returns></returns>
        public bool Equals(DnaConfigurationLiveDataSource other)
        {
            // Return if the sources are the same
            return ConfigurationFileSource.EqualsIgnoreCase(other.ConfigurationFileSource);
        }

        /// <summary>
        /// Gets the hash code for this item
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            // Return the hash code of the Source property
            return ConfigurationFileSource.GetHashCode();
        }

        #endregion
    }
}

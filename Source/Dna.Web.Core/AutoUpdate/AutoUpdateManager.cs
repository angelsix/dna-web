using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dna.Web.Core
{
    /// <summary>
    /// A manager for updating DnaWeb to the latest version automatically
    /// </summary>
    public static class AutoUpdateManager
    {
        /// <summary>
        /// Checks if there is an update available
        /// </summary>
        /// <returns></returns>
        public static bool CheckForUpdate()
        {
            try
            {
                // Our current version and platform
                var currentVersion = DnaSettings.Version;
                var currentPlatform = DnaSettings.Platform;

                // Get version from Internet
                var onlineReleasesString = WebHelpers.DownloadString("http://dnaweb.io/api/releases");
                var onlineReleases = JsonConvert.DeserializeObject<AutoUpdateReleases>(onlineReleasesString);

                // If we have nothing...
                if (onlineReleases == null || onlineReleases.Releases == null || onlineReleases.Releases.Count == 0)
                    // Return
                    return false;

                // Check if we have any newer versions for our platform
                var newerVersion = onlineReleases.Releases
                    // If it is the same platform and newer version...
                    .Where(release => release.Platform == currentPlatform && release.Version > currentVersion)
                    // Order by newest first...
                    .OrderByDescending(release => release.Version)
                    // Try and get one...
                    .FirstOrDefault();

                // If we have a newer version, log it
                if (newerVersion != null)
                {
                    CoreLogger.Log($"** New Version of DnaWeb Available {newerVersion.Version} **", type: LogType.Success);
                    CoreLogger.Log("   Download from http://www.dnaweb.io", type: LogType.Success);

                    return true;

                    // TODO: Auto download and install
                }

                return false;
            }
            catch (Exception ex)
            {
                // Log error
                CoreLogger.Log($"Failed to check for updates. {ex.Message}", type: LogType.Warning);

                // Fail
                return false;
            }

        }
    }
}

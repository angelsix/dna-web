using System;
using System.IO;
using System.Net;

namespace Dna.Web.Core
{
    /// <summary>
    /// Helpers methods for web calls
    /// </summary>
    public static class WebHelpers
    {
        /// <summary>
        /// Attempts to download an URL as a string
        /// Returns null if it fails, and logs failure to log
        /// </summary>
        /// <param name="url">The Url to download</param>
        /// <returns></returns>
        public static string DownloadString(string url)
        {
            try
            {
                // Use WebClient...
                using (var webClient = new WebClient())
                    // Download the Url as a string
                    return webClient.DownloadString(url);
            }
            catch (Exception ex)
            {
                // If we failed, log it
                CoreLogger.Log($"Web Failed to download string '{url}'. {ex.Message}", type: LogType.Warning);

                // Return null
                return null;
            }
        }

        /// <summary>
        /// Attempts to download an URL as a file and save it to the destination path
        /// </summary>
        /// <param name="url">The Url to download</param>
        /// <param name="destinationPath">The destination path to save t o</param>
        /// <returns>True if the download succeeded</returns>
        public static bool DownloadFile(string url, string destinationPath)
        {
            try
            {
                // Make sure destination folder exists
                var directory = Path.GetDirectoryName(destinationPath);

                // Create destination folder if it doesn't exist
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                // Use WebClient...
                using (var webClient = new WebClient())
                    // Download the Url to a file
                    webClient.DownloadFile(url, destinationPath);

                return true;
            }
            catch (Exception ex)
            {
                // If we failed, log it
                CoreLogger.Log($"Web Failed to download file '{url}'. {ex.Message}", type: LogType.Warning);

                // Return fail
                return false;
            }
        }
    }
}

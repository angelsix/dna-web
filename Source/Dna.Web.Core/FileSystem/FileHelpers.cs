using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Dna.Web.Core
{
    /// <summary>
    /// Helpers methods for file systems
    /// </summary>
    public static class FileHelpers
    {
        /// <summary>
        /// A safe way to get all the files in a directory and sub directory without crashing on UnauthorizedException or PathTooLongException
        /// </summary>
        /// <param name="rootPath">Starting directory</param>
        /// <param name="patternMatch">Filename pattern match</param>
        /// <param name="searchOption">Search subdirectories or only top level directory for files</param>
        /// <returns>List of files</returns>
        public static IEnumerable<string> GetDirectoryFiles(string rootPath, string patternMatch)
        {
            // List of found files
            var foundFiles = Enumerable.Empty<string>();

            try
            {
                // If we have no folder
                if (!Directory.Exists(rootPath))
                    // Return empty
                    return foundFiles;

                // Get all directories in this path
                var directories = Directory.EnumerateDirectories(rootPath);

                // For each sub-directory...
                foreach (var directory in directories)
                    // Add files in subdirectories recursively to the list
                    foundFiles = foundFiles.Concat(GetDirectoryFiles(directory, patternMatch));
            }
            // Catch the exceptions we want to ignore
            catch (UnauthorizedAccessException) { }
            catch (PathTooLongException) { }

            // Add files from the current directory
            try { foundFiles = foundFiles.Concat(Directory.EnumerateFiles(rootPath, patternMatch)); }
            catch (UnauthorizedAccessException) { }

            // Return results
            return foundFiles;
        }

        /// <summary>
        /// Checks if the current folder/file exists, and if so appends (n) to the end
        /// until it does not exist. Returns null and logs if it fails
        /// </summary>
        /// <param name="path">The path to check</param>
        /// <returns></returns>
        public static string GetUnusedPath(string path)
        {
            try
            {
                // Count to add if file/folder exists
                var count = 1;

                while (File.Exists(path) || Directory.Exists(path))
                {
                    // Append (n)
                    // So: image.png -> image (1).png -> image (2).png etc...
                    path = Path.Combine(Path.GetDirectoryName(path), $"{Path.GetFileNameWithoutExtension(path)} ({count}).{Path.GetExtension(path)}");
                }

                return path;
            }
            catch (Exception ex)
            {
                // Log it
                CoreLogger.Log($"Failed to get unused path name {path}. {ex.Message}", type: LogType.Warning);

                // Return null
                return null;
            }
        }
    }
}

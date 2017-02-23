using System.IO;

namespace Dna.HtmlEngine.Core
{
    /// <summary>
    /// Handles all the File system IO methods
    /// </summary>
    public static class FileManager
    {
        /// <summary>
        /// Saves the files content to disk
        /// </summary>
        /// <param name="fileContents">The full files contents</param>
        /// <param name="outputName">The absolute path to save the file to</param>
        public static void SaveFile(string fileContents, string outputName)
        {
            File.WriteAllText(outputName, fileContents);
        }

        /// <summary>
        /// Checks if the file exists
        /// </summary>
        /// <param name="path">The full path to the file</param>
        /// <returns></returns>
        public static bool FileExists(string path)
        {
            return File.Exists(path);
        }

        /// <summary>
        /// Reads the files full contents 
        /// </summary>
        /// <param name="path">The full path to the file</param>
        /// <returns></returns>
        public static string ReadAllText(string path)
        {
            return File.ReadAllText(path);
        }
    }
}

using System.IO;
using System.Threading.Tasks;

namespace Dna.Web.Core
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
            var dir = Path.GetDirectoryName(outputName);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

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
        public static async Task<string> ReadAllTextAsync(string path)
        {
            var i = 3;

            // Try to read the file 3 times if it's failed due to a lock
            while (i-- > 0)
            {
                try
                {
                    // Try and read the file
                    return File.ReadAllText(path);
                }
                catch (IOException)
                {
                    // If this is the last attempt, just throw
                    if (i == 0)
                        throw;

                    await Task.Delay(300);
                }
            }

            // NOTE: We can never get here, but VS isn't smart enough to know that. So just throw an IOException if we got here
            throw new IOException();
        }
    }
}

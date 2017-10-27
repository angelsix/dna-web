using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Dna.Web.Core
{
    /// <summary>
    /// An engine that processes the Dna HTML format
    /// </summary>
    public partial class DnaHtmlEngine : DebugEngine
    {
        #region Private Members

        #endregion

        #region Public Properties

        public override string EngineName => "Html";

        #endregion

        #region Constructor

        /// <summary>
        /// Default constructor
        /// </summary>
        public DnaHtmlEngine()
        {
            // Set input extensions
            EngineExtensions = new List<string> { DnaSettings.DnaWebFileExtension };

            // Set output extension
            OutputExtension = ".html";
        }

        #endregion

        protected override Task PreProcessFile(FileProcessingData data)
        {
            return Task.Run(() =>
            {
                // Set this file to partial if it starts with _
                data.IsPartial = Path.GetFileName(data.FullPath).StartsWith("_");
            });
        }
    }
}

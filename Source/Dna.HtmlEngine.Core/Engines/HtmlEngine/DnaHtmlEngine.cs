using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Dna.HtmlEngine.Core
{
    /// <summary>
    /// An engine that processes the Dna HTML format
    /// </summary>
    public partial class DnaHtmlEngine : DebugEngine
    {
        #region Private Members

        #endregion

        #region Constructor

        /// <summary>
        /// Default constructor
        /// </summary>
        public DnaHtmlEngine()
        {
            // Set input extensions
            EngineExtensions = new List<string> { ".dnaweb", "._dnaweb" };

            // Set output extension
            OutputExtension = ".html";
        }

        #endregion
    }
}

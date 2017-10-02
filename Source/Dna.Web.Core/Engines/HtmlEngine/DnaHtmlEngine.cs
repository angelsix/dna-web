using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
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
            EngineExtensions = new List<string> { ".dnaweb", "._dnaweb" };

            // Set output extension
            OutputExtension = ".html";
        }

        #endregion
    }
}

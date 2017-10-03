using System.Xml.Linq;

namespace Dna.Web.Core
{
    /// <summary>
    /// A variable used in an engine for replacing values in a document
    /// </summary>
    public class EngineVariable
    {
        #region Public Properties

        /// <summary>
        /// The profile this variable is assigned to, if any.
        /// Leave blank to be used by default
        /// </summary>
        public string ProfileName { get; set; }

        /// <summary>
        /// The variable name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The variable value
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// The group this variable belongs to, if any
        /// </summary>
        public string Group { get; set; }

        /// <summary>
        /// The comment for this variable
        /// </summary>
        public string Comment { get; set; }

        /// <summary>
        /// The original Xml element of this variable
        /// </summary>
        public XElement XmlElement { get; set; }

        #endregion

        /// <summary>
        /// Debug helper for string name
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return $"{Name}: {Value} {(string.IsNullOrEmpty(ProfileName) ? "" : "[" + ProfileName + "]")}";
        }
    }
}

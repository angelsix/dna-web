namespace Dna.Web.Core
{
    /// <summary>
    /// A DNA Live Data Variable
    /// </summary>
    public class LiveDataSourceVariable
    {
        #region Public Properties

        /// <summary>
        /// The name of the variable
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The file path on the local machine where the contents of this variable reside
        /// </summary>
        public string FilePath { get; set; }

        #endregion
    }
}

namespace Dna.Web.Core
{
    /// <summary>
    /// A DNA Live Data Template
    /// </summary>
    public class LiveDataSourceTemplate
    {
        #region Public Properties

        /// <summary>
        /// The name of the variable
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The file path on the local machine where the contents of this template zip file resides
        /// </summary>
        public string FilePath { get; set; }

        #endregion
    }
}

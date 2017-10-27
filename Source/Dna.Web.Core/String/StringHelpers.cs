namespace Dna.Web.Core
{
    /// <summary>
    /// Helper methods for strings
    /// </summary>
    public static class StringHelpers
    {
        /// <summary>
        /// Checks if one string equals another, ignoring case
        /// </summary>
        /// <param name="firstString">The first string</param>
        /// <param name="otherString">The other string</param>
        /// <returns></returns>
        public static bool EqualsIgnoreCase(this string firstString, string otherString)
        {
            return string.Equals(firstString, otherString, System.StringComparison.InvariantCultureIgnoreCase);
        }
    }
}

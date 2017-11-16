using System.Runtime.Serialization;

namespace Dna.Web.Core
{
    /// <summary>
    /// The current platform for this version of DnaWeb
    /// </summary>
    public enum Platform
    {
        /// <summary>
        /// Unknown platform
        /// </summary>
        Unknown = 0,
        /// <summary>
        /// Windows 32bit
        /// </summary>
        [EnumMember(Value = "win-x32")]
        Windows32 = 1,
        /// <summary>
        /// Windows 64bit
        /// </summary>
        [EnumMember(Value = "win-x64")]
        Windows64 = 2,
        /// <summary>
        /// Linux 32bit
        /// </summary>
        [EnumMember(Value = "linux-x32")]
        Linux32 = 3,
        /// <summary>
        /// Linux 64bit
        /// </summary>
        [EnumMember(Value = "linux-x64")]
        Linux64 = 4,
        /// <summary>
        /// Mac OSX
        /// </summary>
        [EnumMember(Value = "osx")]
        Osx = 5
    }
}

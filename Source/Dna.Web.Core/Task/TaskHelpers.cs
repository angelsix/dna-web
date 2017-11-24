using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Dna.Web.Core
{
    /// <summary>
    /// Helper mehtods for running tasks safer (i.e. not masking errors)
    /// </summary>
    public static class SafeTask
    {
        /// <summary>
        /// A safer way to use Task.Run. The RunSafe catches any errors that occur
        /// and breaks the debugger and logs the error. Without RunSafe, Run would
        /// just silently fail and cause random errors in code and unexpected 
        /// behaviour
        /// </summary>
        /// <param name="action"></param>
        public static Task Run(Action action, [CallerMemberName]string memberName = "")
        {
            return Task.Run(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Debugger.Break();

                    CoreLogger.Log($"Unhandled exception from Task.RunSafe of {memberName}. {ex.Message}");

                    // Throw original error as as to not change the flow of code
                    // that would of happened with a standard Task.Run
                    throw;
                }
            });
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text;

namespace Dna.Web.Core
{
    /// <summary>
    /// A core logger for logging messages from anywhere in the system
    /// </summary>
    public static class CoreLogger
    {
        #region Private Members

        /// <summary>
        /// Object to lock Log writes
        /// </summary>
        private static object LogLock = new object();

        #endregion

        #region Public Properties

        /// <summary>
        /// The log level for output
        /// </summary>
        public static LogLevel LogLevel { get; set; } = LogLevel.All;

        #endregion

        #region Public Methods

        /// <summary>
        /// Logs a message and raises the <see cref="Write"/> event
        /// </summary>
        /// <param name="title">The title of the log</param>
        /// <param name="message">The main message of the log</param>
        /// <param name="type">The type of the log message</param>
        public static void Log(string title, string message = "", LogType type = LogType.Diagnostic)
        {
            Write(new LogMessage
            {
                Title = title,
                Message = message,
                Time = DateTime.UtcNow,
                Type = type
            }, LogLevel);
        }

        /// <summary>
        /// Logs a message and raises the <see cref="Write"/> event
        /// Logs a name and value at a certain tab level to simulate a sub-item of another log
        /// 
        /// For example: LogTabbed("Name", "Value", 1);
        /// ----Name: Value
        /// Where ---- are spaces
        /// </summary>
        /// <param name="name">The name to log</param>
        /// <param name="value">The value to log</param>
        /// <param name="tabLevel"></param>
        /// <param name="type">The type of the log message</param>
        public static void LogTabbed(string name, string value, int tabLevel, LogType type = LogType.Diagnostic)
        {
            // Add 4 spaces per tab level
            Log($"{"".PadLeft(tabLevel * 4, ' ')}{name}{(string.IsNullOrEmpty(value) ? "" : (": " + value))}", type: type);
        }

        /// <summary>
        /// Logs a message to the console
        /// </summary>
        /// <param name="message">The message to log</param>
        /// <param name="logLevel">The current log level setting</param>
        public static void Write(this LogMessage message, LogLevel logLevel)
        {
            lock (LogLock)
            {
                // If the log level is less than the log type...
                if ((int)logLevel < (int)message.Type)
                    // Don't log
                    return;

                // Set color
                switch (message.Type)
                {
                    case LogType.Diagnostic:
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        break;

                    case LogType.Error:
                        Console.ForegroundColor = ConsoleColor.Red;
                        break;

                    case LogType.Information:
                        Console.ForegroundColor = ConsoleColor.White;
                        break;

                    case LogType.Attention:
                        Console.ForegroundColor = ConsoleColor.Blue;
                        break;

                    case LogType.Success:
                        Console.ForegroundColor = ConsoleColor.Green;
                        break;

                    case LogType.Warning:
                        Console.ForegroundColor = ConsoleColor.DarkYellow;
                        break;

                    default:
                        Console.ResetColor();
                        break;
                }

                // Get current time
                var time = DateTime.Now.ToLongTimeString();

                // Output title
                Console.WriteLine($"[{time}] {message.Title}");

                // Output detailed message if we have one
                if (!string.IsNullOrEmpty(message.Message))
                    Console.WriteLine(message.Message);

                // Clear color
                Console.ResetColor();
            }
        }

        #endregion
    }
}

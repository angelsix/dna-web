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
        /// <param name="newLine">True if it should write a full line, false to write on the current line</param>
        /// <param name="faded">If true, the text is faded regardless of type</param>
        /// <param name="noTime">If true, the current time is not output at the start</param>
        public static void Log(string title, string message = "", LogType type = LogType.Diagnostic, bool newLine = true, bool faded = false, bool noTime = false)
        {
            Write(new LogMessage
            {
                Title = title,
                Message = message,
                Time = DateTime.UtcNow,
                Type = type
            }, LogLevel, newLine, faded, noTime);
        }

        /// <summary>
        /// Logs a message and raises the <see cref="Write"/> event, as the type <see cref="LogType.Information"/>
        /// </summary>
        /// <param name="title">The title of the log</param>
        /// <param name="message">The main message of the log</param>
        /// <param name="newLine">True if it should write a full line, false to write on the current line</param>
        /// <param name="faded">If true, the text is faded regardless of type</param>
        /// <param name="noTime">If true, the current time is not output at the start</param>
        public static void LogInformation(string title, string message = "", bool newLine = true, bool faded = false, bool noTime = false)
        {
            Write(new LogMessage
            {
                Title = title,
                Message = message,
                Time = DateTime.UtcNow,
                Type = LogType.Information
            }, LogLevel, newLine, faded, noTime);
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
        /// <param name="newLine">True if it should write a full line, false to write on the current line</param>
        /// <param name="faded">If true, the text is faded regardless of type</param>
        /// <param name="noTime">If true, the current time is not output at the start</param>
        public static void LogTabbed(string name, string value, int tabLevel, LogType type = LogType.Diagnostic, bool newLine = true, bool faded = false, bool noTime = false)
        {
            // Add 4 spaces per tab level
            Log($"{"".PadLeft(tabLevel * 4, ' ')}{name}{(string.IsNullOrEmpty(value) ? "" : (": " + value))}", type: type, newLine: newLine, faded: faded, noTime: noTime);
        }

        /// <summary>
        /// Logs a message to the console
        /// </summary>
        /// <param name="message">The message to log</param>
        /// <param name="logLevel">The current log level setting</param>
        /// <param name="newLine">True if it should write a full line, false to write on the current line</param>
        /// <param name="faded">If true, the text is faded regardless of type</param>
        /// <param name="noTime">If true, the current time is not output at the start</param>
        public static void Write(this LogMessage message, LogLevel logLevel, bool newLine = true, bool faded = false, bool noTime = false)
        {
            lock (LogLock)
            {
                // If the log level is less than the log type...
                if ((int)logLevel < (int)message.Type)
                    // Don't log
                    return;

                // Get type for color
                var type = faded ? LogType.Diagnostic : message.Type;

                // Set color
                switch (type)
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
                var prefix = noTime ? string.Empty : $"[{DateTime.Now.ToLongTimeString()}] ";

                // Output title
                // If we want a new line or we have a description...
                if (newLine || !string.IsNullOrEmpty(message.Message))
                    // Write line
                    Console.WriteLine($"{prefix}{message.Title}");
                // Otherwise...
                else
                    // Write on same line
                    Console.Write($"{prefix}{message.Title}");

                // Output detailed message if we have one
                if (!string.IsNullOrEmpty(message.Message))
                {
                    // If new line
                    if (newLine)
                        // Write line
                        Console.WriteLine(message.Message);
                    // Otherwise
                    else
                        // Write on same line
                        Console.Write(message.Message);
                }

                // Clear color
                Console.ResetColor();
            }
        }

        #endregion
    }
}

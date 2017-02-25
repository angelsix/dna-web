using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Dna.HtmlEngine.Core
{
    /// <summary>
    /// An engine that processes the Dna C# format
    /// </summary>
    public partial class DnaCSharpEngine : DebugEngine
    {
        #region Protected Members

        /// <summary>
        /// The regex to match special tags containing a name and a value
        /// For example: <!--# properties groupd=DomIds; value=Something with a space; #--> 
        /// Group 1 would be properties
        /// Group 2 would be groupd=DomIds; value=Something with a space; which you can string split via ; and trim
        /// </summary>
        protected string mCSharp2GroupRegex = @"<!--#\s*(\w+)\s*(.*?)\s*#-->";

        #endregion

        #region Constructor

        /// <summary>
        /// Default constructor
        /// </summary>
        public DnaCSharpEngine()
        {
            // Set input extensions
            EngineExtensions = new List<string> { ".dnacs" };

            // Set output extension
            OutputExtension = ".cs";
        }

        #endregion

        /// <summary>
        /// Replace C# specific tags
        /// </summary>
        /// <param name="data">The file data</param>
        /// <returns></returns>
        protected override Task PostProcessFile(FileProcessingData data)
        {
            return Task.Run(() =>
            {
                // For each output
                data.OutputPaths.ForEach(output =>
                {
                    // Find all C# data sets
                    Match match = null;

                    // No error to start with
                    data.Error = string.Empty;

                    // Loop through all matches
                    while (match == null || match.Success)
                    {
                        // Find the next data region
                        match = Regex.Match(output.FileContents, mCSharp2GroupRegex, RegexOptions.Singleline);

                        if (!match.Success)
                            return;

                        // NOTE: The first group is the full match
                        //       The second group is the type
                        //       Third group is data

                        // Make sure we have enough groups
                        if (match.Groups.Count < 3)
                        {
                            data.Error = $"Malformed match {match.Value}";
                            return;
                        }

                        // Take the first match as the header for the type of tag
                        var type = match.Groups[1].Value.Trim();
                        var datasString = match.Groups[2].Value.Trim();

                        // Split the data via ; and trim it
                        // Example data would be: group=DomIds; value=Something with space; 
                        List<CSharpTagDataItem> propertyData = null;

                        try
                        {
                            propertyData = datasString.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(f =>
                            {
                                // Get data string
                                var dataString = f?.Trim();

                                // Make sure we have at least one = after a character
                                if (dataString.IndexOf('=') <= 0)
                                    throw new ArgumentException("Invalid CSharp data item: " + dataString);

                                // Get name and value
                                var name = dataString.Substring(0, dataString.IndexOf('='));
                                var value = dataString.Substring(dataString.IndexOf('=') + 1);

                                return new CSharpTagDataItem
                                {
                                    Name = name,
                                    Value = value
                                };

                            }).ToList();
                        }
                        catch (Exception ex)
                        {
                            data.Error = ex.Message;
                            return;
                        }

                        switch (type)
                        {
                            case "properties":

                                // Process the property data
                                ProcessPropertiesTab(data, output, match, propertyData);

                                // Check for success
                                if (!data.Successful)
                                    // Return if it fails
                                    return;

                                break;

                            default:
                                // Report error of unknown match
                                data.Error = $"Unknown match {match.Value}";
                                return;
                        }
                    }
                });
            });
        }

        /// <summary>
        /// Processes a properties tag, replacing it with the variabes found in the given data group
        /// </summary>
        /// <param name="data">The file processing data</param>
        /// <param name="output">The file output data</param>
        /// <param name="match">The original match that found this information</param>
        /// <param name="propertyData">The properties data</param>
        protected void ProcessPropertiesTab(FileProcessingData data, FileOutputData output, Match match, List<CSharpTagDataItem> propertyData)
        {
            // No error to start with
            data.Error = string.Empty;

            // Get group
            var group = propertyData.FirstOrDefault(f => string.Equals(f.Name, "group", StringComparison.CurrentCultureIgnoreCase))?.Value;

            // Find all variables within the specific group
            // or all variables not in a group if nonne is specified
            var variables = output.Variables.Where(f => string.Equals(f.Group, group)).ToList();

            // Find the indentation level
            // Based on the number of whitespaces before the match
            var indentation = 0;
            var i = match.Index - 1;
            while (i > 0 && output.FileContents[i--] == ' ')
                indentation++;

            // Create indentation string
            var indentString = "".PadLeft(indentation, ' ');

            // Remove the original tag
            ReplaceTag(output, match, string.Empty, removeNewline: false);

            // If there are no variables, just end
            if (variables.Count == 0 )
                return;

            // Start region (doesn't need indentation as the original is there)
            var result = $"#region {group}\r\n\r\n";

            // For each variable, convert it to a property
            variables.ForEach(variable =>
            {
                // Don't continue if we have an error
                if (!data.Successful)
                    return;

                // Add the comment
                var property = (string.IsNullOrEmpty(variable.Comment) ? indentString : GenerateXmlComment(variable.Comment, indentString));

                // Get the type
                var type = variable.XmlElement.Attribute("Type")?.Value ?? variable.XmlElement.Element("Type")?.Value;

                // Make sure we got a type
                if (string.IsNullOrEmpty(type))
                {
                    data.Error = $"Variable has not specified a type. {variable.XmlElement.ToString()}";
                    return;
                }

                // Add property, leaving a newline for the next property
                property += $"{indentString}public {type} {variable.Name} {{ get; set; }}{ GenerateValueSetter(type, variable.Value) }\r\n\r\n";

                // Add this to the result
                result += property;
            });

            // Mare sure we had no issues
            if (!data.Successful)
                return;

            // End region
            result += $"{indentString}#endregion\r\n";

            // Inject this at the match location
            output.FileContents = output.FileContents.Insert(match.Index, result);
        }


        /// <summary>
        /// Generates a C# variable setter for the given type
        /// For example, a string type and a value test would
        /// return "test"
        /// </summary>
        /// <param name="type">The C# type</param>
        /// <param name="value">The value to wrap</param>
        /// <returns></returns>
        protected string GenerateValueSetter(string type, string value)
        {
            switch (type.ToLower())
            {
                case "string":
                    return $" = \"{value}\";";

                // If we don't know the type just return the value
                default:
                    return $" = {value};";
            }
        }

        /// <summary>
        /// Converts a string comment to an XML comment
        /// </summary>
        /// <param name="comment">The string comment to convert</param>
        /// <param name="indentString">The indentation string to add before each line</param>
        /// <returns></returns>
        private string GenerateXmlComment(string comment, string indentString)
        {
            // Remove carriage returns, so we just have newlines
            comment = comment.Replace("\r", "");

            var result = $"{indentString}/// <summary>";

            // Get each line
            var lines = new List<string>(comment.Split('\n'));

            // Ignore empty lines if this is the last line
            if (lines.Count > 1 && string.IsNullOrWhiteSpace(lines[lines.Count - 1]))
                lines = lines.Take(lines.Count - 1).ToList();
            // Ignore empty lines if this is the first line
            if (lines.Count > 1 && string.IsNullOrWhiteSpace(lines[0]))
                lines = lines.Skip(1).Take(lines.Count - 1).ToList();

            // If every comment has at least the indent number of spaces
            // Presume they are doing a multi-line comment and remove them
            //
            // e.g.
            //        < !--
            //        This is a comment for one
            //        second
            //        third
            //        -- >
            //
            // Then we should remove the spaces from the start of the comment
            if (lines.All(c => c.Length >= indentString.Length && c.Take(indentString.Length).All(cc => cc == ' ')))
                lines = lines.Select(f => f.Substring(indentString.Length)).ToList();

            // For each line
            for (var i = 0; i < lines.Count; i++)
            {
                // Get the current line
                var line = lines[i];

                // Ignore empty lines if this is the first or last
                if ((i == 0 || i == lines.Count - 1) && string.IsNullOrWhiteSpace(line))
                    continue;

                result += $"\r\n{indentString}/// {line}";
            }

            result += $"\r\n{indentString}/// </summary>\r\n";

            return result;
        }
    }
}

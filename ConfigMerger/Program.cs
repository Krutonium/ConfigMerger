using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using System.Xml.Linq;
using System.Configuration;
using System.Text;
using System.Xml;
using Newtonsoft.Json.Linq;
using Formatting = Newtonsoft.Json.Formatting;

namespace ConfigMerger
{
    class Program
    {
        
        /// <summary>
        /// This programs goal is to merge arbitrary config file formats together. Initially it supports Ini, Json, and XML.
        /// File1 should be overwritten if outputFile is not specified. (File 2 is interpreted as a "patch" of sorts.
        /// Feel free to PR new file formats or refactoring, and especially bug fixes :D
        /// </summary>
        
        
        public static string TemporaryFile = Path.GetTempFileName();

        static void Main(string[] args)
        {
            string file1 = "";
            string file2 = "";
            string fileType = "";
            string outputFile = "";
            bool help = false;

            // Parse command line arguments
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-o":
                    case "--original":
                        file1 = args[++i];
                        break;
                    case "-y":
                    case "--overlay":
                        file2 = args[++i];
                        break;
                    case "-t":
                    case "--type":
                        fileType = args[++i];
                        break;
                    case "-O":
                    case "--output":
                        outputFile = args[++i];
                        break;
                    case "-h":
                    case "--help":
                        help = true;
                        break;
                    default:
                        Console.WriteLine($"Unknown option: {args[i]}");
                        help = true;
                        break;
                }
            }

            if (help || string.IsNullOrEmpty(file1) || string.IsNullOrEmpty(file2))
            {
                Console.WriteLine("Usage: FileOverlay [options] -o file1 -y file2");
                Console.WriteLine("Options:");
                Console.WriteLine("  -o, --original FILE    Specify the original file (required)");
                Console.WriteLine("  -y, --overlay FILE     Specify the overlay file (required)");
                Console.WriteLine("  -t, --type TYPE        Specify the file type (json, xml, ini)");
                Console.WriteLine("  -O, --output FILE      Specify the output file (defaults to file1)");
                Console.WriteLine("  -h, --help             Show this help message");
                return;
            }

            if (string.IsNullOrEmpty(outputFile))
            {
                outputFile = file1;
            }

            if (string.IsNullOrEmpty(fileType))
            {
                fileType = Path.GetExtension(file2).ToLower();
            }
            else
            {
                fileType = "." + fileType.ToLower();
            }

            if (fileType == ".json")
            {
                OverlayJson(file1, file2, outputFile);
            }
            else if (fileType == ".xml")
            {
                OverlayXml(file1, file2, outputFile);
            }
            else if (fileType == ".ini")
            {
                OverlayIni(file1, file2, outputFile);
            }
            else
            {
                Console.WriteLine($"Unknown file type: {fileType}");
                return;
            }

            Console.WriteLine($"Overlay complete. Output written to {outputFile}");
        }

        #region  Ini
        public static void OverlayIni(string file1, string file2, string outputFile)
        {
            Dictionary<string, Dictionary<string, string>> sections1 = ParseIniFile(file1);
            Dictionary<string, Dictionary<string, string>> sections2 = ParseIniFile(file2);

            foreach (var section in sections2)
            {
                if (!sections1.ContainsKey(section.Key))
                {
                    sections1.Add(section.Key, section.Value);
                }
                else
                {
                    foreach (var keyValue in section.Value)
                    {
                        sections1[section.Key][keyValue.Key] = keyValue.Value;
                    }
                }
            }

            using (var writer = new StreamWriter(TemporaryFile, false, Encoding.UTF8))
            {
                foreach (var section in sections1)
                {
                    writer.WriteLine($"[{section.Key}]");

                    foreach (var keyValue in section.Value)
                    {
                        writer.WriteLine($"{keyValue.Key}={keyValue.Value}");
                    }

                    writer.WriteLine();
                }
                writer.Close();
            }
            File.Move(TemporaryFile, outputFile);
        }

        private static Dictionary<string, Dictionary<string, string>> ParseIniFile(string fileName)
        {
            var result = new Dictionary<string, Dictionary<string, string>>();
            var currentSection = new Dictionary<string, string>();

            foreach (var line in File.ReadLines(fileName))
            {
                string trimmedLine = line.Trim();

                if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                {
                    string sectionName = trimmedLine.Substring(1, trimmedLine.Length - 2);
                    currentSection = new Dictionary<string, string>();
                    result.Add(sectionName, currentSection);
                }
                else if (!string.IsNullOrEmpty(trimmedLine) && !trimmedLine.StartsWith(";"))
                {
                    int equalIndex = trimmedLine.IndexOf('=');

                    if (equalIndex > 0)
                    {
                        string key = trimmedLine.Substring(0, equalIndex).Trim();
                        string value = trimmedLine.Substring(equalIndex + 1).Trim();
                        currentSection.Add(key, value);
                    }
                }
            }

            return result;
        }
        #endregion

        #region Xml
        private static void OverlayXml(string file1, string file2, string outputFile)
        {
            // Create XmlReader instances for both input files
            XmlReader reader1 = XmlReader.Create(file1);
            XmlReader reader2 = XmlReader.Create(file2);

            // Create XmlWriter instance for the output file
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = true; // make the output file indented
            XmlWriter writer = XmlWriter.Create(TemporaryFile, settings);

            // Write the root element to the output file
            writer.WriteStartElement("root");

            // Read and write the elements from the first input file
            while (reader1.Read())
            {
                if (reader1.NodeType == XmlNodeType.Element)
                {
                    writer.WriteNode(reader1, true);
                }
            }

            // Read and write the elements from the second input file
            while (reader2.Read())
            {
                if (reader2.NodeType == XmlNodeType.Element)
                {
                    writer.WriteNode(reader2, true);
                }
            }

            // Close the root element
            writer.WriteEndElement();

            // Close the XmlWriter and XmlReader instances
            writer.Close();
            reader1.Close();
            reader2.Close();
            File.Move(TemporaryFile, outputFile, true);
        }
        #endregion
        
        #region Json
        static void OverlayJson(string file1, string file2, string outputFile)
        {
            // Load the JSON files
            JObject json1 = JObject.Parse(File.ReadAllText(file1));
            JObject json2 = JObject.Parse(File.ReadAllText(file2));

            // Merge the JSON files
            json1.Merge(json2, new JsonMergeSettings
            {
                // If a property with the same name already exists, overwrite it
                MergeArrayHandling = MergeArrayHandling.Replace,
                MergeNullValueHandling = MergeNullValueHandling.Merge,
                PropertyNameComparison = StringComparison.OrdinalIgnoreCase
            });

            // Write the merged JSON to a file
            using (StreamWriter file = File.CreateText(TemporaryFile))
            {
                JsonSerializer serializer = new JsonSerializer();
                serializer.Formatting = Formatting.Indented;
                serializer.Serialize(file, json1);
                file.Close();
            }

            File.Move(TemporaryFile, outputFile, true);
        }
        #endregion
    }
}
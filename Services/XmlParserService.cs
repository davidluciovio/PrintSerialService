using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace ZebraPrintUtility.Services
{
    public class XmlParserService
    {
        /// <summary>
        /// Parses an XML file into a list of label records, where each record is a dictionary of fields and values.
        /// </summary>
        public List<Dictionary<string, string>> ParseXmlToRecords(string xmlContent)
        {
            var records = new List<Dictionary<string, string>>();

            try
            {
                XDocument doc = XDocument.Parse(xmlContent);
                XElement? root = doc.Root;

                if (root == null) return records;

                // Detect if we have repeating child elements (e.g., <Row>, <Record>, <Label>)
                // We'll look for the first level of child elements that have duplicates.
                var repeatingGroups = root.Elements()
                    .GroupBy(e => e.Name.LocalName)
                    .Where(g => g.Count() > 1)
                    .ToList();

                if (repeatingGroups.Any())
                {
                    // Case 1: We have repeating elements (like a list of rows/labels)
                    var groupName = repeatingGroups.First().Key;
                    var recordElements = root.Elements(root.Name.Namespace + groupName);

                    foreach (var recordElem in recordElements)
                    {
                        var recordDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        FlattenElement(recordElem, recordDict, "");
                        if (recordDict.Count > 0)
                        {
                            records.Add(recordDict);
                        }
                    }
                }
                else
                {
                    // Case 2: No obvious repeating groups at the first level.
                    // We can check if there are nested repeating groups, or treat the entire XML as a single record.
                    var recordDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    
                    // Let's check if there is any other repeating element deeper down
                    var allElements = root.Descendants().ToList();
                    var duplicateNames = allElements
                        .GroupBy(e => e.Name.LocalName)
                        .Where(g => g.Count() > 1)
                        .Select(g => g.Key)
                        .ToList();

                    if (duplicateNames.Any())
                    {
                        // Find the parent of the first duplicate name
                        var dupName = duplicateNames.First();
                        var dupElements = root.Descendants().Where(e => e.Name.LocalName == dupName).ToList();
                        var commonParent = dupElements.First().Parent;

                        if (commonParent != null && commonParent.Elements(commonParent.Name.Namespace + dupName).Count() > 1)
                        {
                            // Treat these common parent's children as the records
                            foreach (var recordElem in commonParent.Elements(commonParent.Name.Namespace + dupName))
                            {
                                var subDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                                FlattenElement(recordElem, subDict, "");
                                if (subDict.Count > 0)
                                {
                                    records.Add(subDict);
                                }
                            }
                            return records;
                        }
                    }

                    // Otherwise, just treat the whole file as a single record
                    FlattenElement(root, recordDict, "");
                    if (recordDict.Count > 0)
                    {
                        records.Add(recordDict);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidDataException($"Failed to parse XML: {ex.Message}", ex);
            }

            return records;
        }

        /// <summary>
        /// Recursively flattens XML elements into a dictionary.
        /// If a node has child elements, it explores them.
        /// If it is a leaf, it adds its LocalName and Value to the dictionary.
        /// </summary>
        private void FlattenElement(XElement element, Dictionary<string, string> dict, string prefix)
        {
            string currentKey = string.IsNullOrEmpty(prefix) 
                ? element.Name.LocalName 
                : $"{prefix}_{element.Name.LocalName}";

            // If the element has no child elements, it's a leaf node. Add its value.
            if (!element.HasElements)
            {
                string value = element.Value.Trim();
                // Add the simple name (local name) directly if it does not exist, or append/overwrite
                string localName = element.Name.LocalName;
                
                // Add localName directly (preferred for simple ZPL replacement like {ItemCode})
                if (!dict.ContainsKey(localName))
                {
                    dict[localName] = value;
                }
                
                // Also add with prefix (e.g. Row_ItemCode) to ensure uniqueness if needed
                if (!string.Equals(localName, currentKey, StringComparison.OrdinalIgnoreCase))
                {
                    dict[currentKey] = value;
                }
            }
            else
            {
                // Process child elements
                foreach (var child in element.Elements())
                {
                    FlattenElement(child, dict, prefix == "" ? element.Name.LocalName : currentKey);
                }
            }

            // Also check and add Attributes if any
            foreach (var attr in element.Attributes())
            {
                string attrKey = $"{element.Name.LocalName}_{attr.Name.LocalName}";
                if (!dict.ContainsKey(attrKey))
                {
                    dict[attrKey] = attr.Value.Trim();
                }
            }
        }

        /// <summary>
        /// Merges a dictionary of key-value pairs into a ZPL template.
        /// Replaces placeholders like {Key} or [Key] with corresponding values.
        /// </summary>
        public string MergeZplTemplate(string zplTemplate, Dictionary<string, string> values)
        {
            if (string.IsNullOrEmpty(zplTemplate)) return string.Empty;

            string merged = zplTemplate;
            foreach (var kvp in values)
            {
                // Support multiple placeholder formats: {Key}, {key}, [Key], [key]
                merged = merged.Replace($"{{{kvp.Key}}}", kvp.Value, StringComparison.OrdinalIgnoreCase);
                merged = merged.Replace($"[{kvp.Key}]", kvp.Value, StringComparison.OrdinalIgnoreCase);
            }
            return merged;
        }
    }
}

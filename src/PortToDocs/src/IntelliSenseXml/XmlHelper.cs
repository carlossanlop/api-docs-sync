// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace ApiDocsSync.Libraries.IntelliSenseXml
{
    internal class XmlHelper
    {
        private static readonly Dictionary<string, string> _replaceableExceptionPatterns = new Dictionary<string, string>{
            { "<para>",  "\r\n" },
            { "</para>", "" }
        };

        internal static string ReplaceExceptionPatterns(string value)
        {
            string updatedValue = value;
            foreach (KeyValuePair<string, string> kvp in _replaceableExceptionPatterns)
            {
                if (updatedValue.Contains(kvp.Key))
                {
                    updatedValue = updatedValue.Replace(kvp.Key, kvp.Value);
                }
            }

            updatedValue = Regex.Replace(updatedValue, @"[\r\n\t ]+\-[ ]?or[ ]?\-[\r\n\t ]+", "\r\n\r\n-or-\r\n\r\n");
            return updatedValue;
        }

        internal static string GetNodesInPlainText(XElement element)
        {
            if (element == null)
            {
                throw new Exception("A null element was passed when attempting to retrieve the nodes in plain text.");
            }

            // string.Join("", element.Nodes()) is very slow.
            //
            // The following is twice as fast (although still slow)
            // but does not produce the same spacing. That may be OK.
            //
            //using var reader = element.CreateReader();
            //reader.MoveToContent();
            //return reader.ReadInnerXml().Trim();

            return string.Join("", element.Nodes()).Trim();
        }

        internal static string GetAttributeValue(XElement parent, string name)
        {
            if (parent == null)
            {
                throw new Exception($"A null parent was passed when attempting to get attribute '{name}'");
            }
            else
            {
                XAttribute? attr = parent.Attribute(name);
                if (attr != null)
                {
                    return attr.Value.Trim();
                }
            }
            return string.Empty;
        }

        internal static bool TryGetChildElement(XElement parent, string name, out XElement? child)
        {
            child = null;

            if (parent == null || string.IsNullOrWhiteSpace(name))
                return false;

            child = parent.Element(name);

            return child != null;
        }
    }
}
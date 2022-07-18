// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace ApiDocsSync.Libraries.IntelliSenseXml
{
    internal static class Extensions
    {
        // Some API DocIDs with types contain "{" and "}" to enclose the typeparam, which causes
        // an exception to be thrown when trying to embed the string in a formatted string.
        internal static string DocIdEscaped(this string docId) =>
            docId
            .Replace("{", "{{")
            .Replace("}", "}}")
            .Replace("<", "{{")
            .Replace(">", "}}")
            .Replace("&lt;", "{{")
            .Replace("&gt;", "}}");

        // Adds a string to a list of strings if the element is not there yet. The method makes sure to escape unexpected curly brackets to prevent formatting exceptions.
        internal static void AddIfNotExists(this List<string> list, string element)
        {
            string cleanedElement = element.DocIdEscaped();
            if (!list.Contains(cleanedElement))
            {
                list.Add(cleanedElement);
            }
        }

        // Checks if the passed string is considered "empty" according to the intellisense xml repo rules.
        internal static bool IsIntelliSenseEmpty(this string? s) => string.IsNullOrWhiteSpace(s);

        // Removes the specified substrings from another string
        internal static string RemoveSubstrings(this string oldString, params string[] stringsToRemove)
        {
            string newString = oldString;
            foreach (string toRemove in stringsToRemove)
            {
                if (newString.Contains(toRemove))
                {
                    newString = newString.Replace(toRemove, string.Empty);
                }
            }
            return newString;
        }

    }
}
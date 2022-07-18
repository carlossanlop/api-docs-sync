// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace ApiDocsSync.Libraries.Docs
{
    internal static class Extensions
    {
        internal static bool ContainsStrings(this string text, string[] strings)
        {
            foreach (string str in strings)
            {
                if (text.Contains(str))
                {
                    return true;
                }
            }

            return false;
        }

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

        // Checks if the passed string is considered "empty" according to the Docs repo rules.
        internal static bool IsDocsEmpty(this string? s) =>
            string.IsNullOrWhiteSpace(s) || s == DocsAPI.ToBeAdded;
    }

}

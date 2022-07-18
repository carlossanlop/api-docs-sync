// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;

namespace ApiDocsSync.Libraries.Docs
{
    public class DocsConfiguration
    {
        public DocsConfiguration()
        {
            DirsDocsXml = new List<DirectoryInfo>();
            ExcludedAssemblies = new HashSet<string>();
            ExcludedNamespaces = new HashSet<string>();
            ExcludedTypes = new HashSet<string>();
            IncludedAssemblies = new HashSet<string>();
            IncludedNamespaces = new HashSet<string>();
            IncludedTypes = new HashSet<string>();
        }

        public List<DirectoryInfo> DirsDocsXml { get; }
        public HashSet<string> ExcludedAssemblies { get; }
        public HashSet<string> ExcludedNamespaces { get; }
        public HashSet<string> IncludedAssemblies { get; }
        public HashSet<string> IncludedNamespaces { get; }
        public HashSet<string> ExcludedTypes { get; }
        public HashSet<string> IncludedTypes { get; }
        public bool Save { get; set; }
        public bool SkipInterfaceImplementations { get; set; }
    }
}
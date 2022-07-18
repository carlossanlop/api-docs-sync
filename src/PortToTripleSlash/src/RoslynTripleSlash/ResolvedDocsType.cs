// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using ApiDocsSync.Libraries.Docs;

namespace ApiDocsSync.Libraries.RoslynTripleSlash
{
    public class ResolvedDocsType
    {
        public DocsType DocsType { get; }
        public List<ResolvedLocation>? SymbolLocations { get; }

        public ResolvedDocsType(DocsType docsType, List<ResolvedLocation> symbolLocations)
        {
            DocsType = docsType;
            SymbolLocations = symbolLocations;
        }
    }
}
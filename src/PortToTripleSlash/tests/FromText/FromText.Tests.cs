// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ApiDocsSync.Libraries.Docs;
using ApiDocsSync.Libraries.RoslynTripleSlash;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using Xunit.Abstractions;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Xml.Linq;
using System.Text;

namespace ApiDocsSync.Libraries.Tests
{
    public class FromTextTests : BasePortTests
    {
        private const string NetVersion = "net7.0";
        private const string CsprojContents =
            "<Project Sdk=\"Microsoft.NET.Sdk\">" +
                "<PropertyGroup>" +
                    "<OutputType>Library</OutputType>" +
                    $"<TargetFramework>{NetVersion}</TargetFramework>" +
                "</PropertyGroup>" +
            "</Project>";

        public FromTextTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void Test()
        {
            string tripleSlashText = "";
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(tripleSlashText);
        }

        private static async Task PortToTripleSlashAsync(Dictionary<string, string> docs, bool skipInterfaceImplementations = true)
        {
            Configuration c = new()
            {
                SkipInterfaceImplementations = skipInterfaceImplementations,
                BinLogPath = "TODO"
            };

            CancellationTokenSource cts = new();

            VSLoader.LoadVSInstance();
            c.Loader = new MSBuildLoader(c.BinLogPath);

            await c.Loader.LoadMainProjectAsync(c.CsProj, c.IsMono, cts.Token);

            ToTripleSlashPorter porter = new(c);

            foreach ((string fileName, string contents) in docs)
            {
                XDocument xDocs = XDocument.Parse(contents);
                var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
                porter.LoadDocsFile(xDocs, fileName, encoding: utf8NoBom);
            }

            // Log.Info("Iterating the docs xml files...");
            // foreach (FileInfo fileInfo in porter.EnumerateDocsXmlFiles())
            // {
            //     Log.Info($"Attempting to load xml file '{fileInfo.FullName}'...");
            //     DocsType? docsType = porter.LoadDocsTypeForFile(fileInfo);
            //     if (docsType == null)
            //     {
            //         Log.Error($"Malformed file.");
            //         continue;
            //     }

            //     Log.Success($"File loaded successfully.");
            //     Log.Info($"Looking for symbol locations for {docsType.TypeName}...");
            //     List<ResolvedLocation>? symbolLocations = await porter.CollectSymbolLocationsAsync(docsType.TypeName, cts.Token).ConfigureAwait(false);
            //     if (symbolLocations == null)
            //     {
            //         Log.Error("No symbols found.");
            //         continue;
            //     }
            //     Log.Info($"Finished looking for symbol locations for {docsType.TypeName}. Now attempting to port...");
            //     ResolvedDocsType rdt = new(docsType, symbolLocations);
            //     await porter.PortAsync(rdt, throwOnSymbolsNotFound: true, cts.Token).ConfigureAwait(false);
            // }

            Verify();
        }


        private static void Verify()
        {
        }
    }
}
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ApiDocsSync.Libraries.Docs;
using ApiDocsSync.Libraries.RoslynTripleSlash;
using Xunit;
using Xunit.Abstractions;

namespace ApiDocsSync.Libraries.Tests
{
    public class FromFilesTests : BasePortTests
    {
        public FromFilesTests(ITestOutputHelper output) : base(output)
        {
        }

        // Tests failing due to: https://github.com/dotnet/roslyn/issues/61454

        // Project.OpenProjectAsync - C:\Users\carlos\AppData\Local\Temp\dmeyjbwb.vtc\Project\MyAssembly.csproj
        // Failure - Msbuild failed when processing the file 'C:\Users\carlos\AppData\Local\Temp\dmeyjbwb.vtc\Project\MyAssembly.csproj' with message: C:\Program Files\dotnet\sdk\6.0.302\Sdks\Microsoft.NET.Sdk\targets\Microsoft.NET.Sdk.FrameworkReferenceResolution.targets: (90, 5): The "ProcessFrameworkReferences" task failed unexpectedly.
        // System.IO.FileLoadException: Could not load file or assembly 'NuGet.Frameworks, Version=6.2.1.7, Culture=neutral, PublicKeyToken=31bf3856ad364e35'.Could not find or load a specific file. (0x80131621)
        // File name: 'NuGet.Frameworks, Version=6.2.1.7, Culture=neutral, PublicKeyToken=31bf3856ad364e35'

        [Fact]
        public Task Port_Basic() => PortToTripleSlashAsync("Basic");

        [Fact]
        public Task Port_Generics() => PortToTripleSlashAsync("Generics");

        private static async Task PortToTripleSlashAsync(
            string testDataDir,
            bool skipInterfaceImplementations = true,
            string assemblyName = TestData.TestAssembly,
            string namespaceName = TestData.TestNamespace)
        {
            using TestDirectory tempDir = new();

            TestData testData = new(
                tempDir,
                testDataDir,
                assemblyName,
                namespaceName);

            Configuration c = new()
            {
                CsProj = Path.GetFullPath(testData.ProjectFilePath),
                SkipInterfaceImplementations = skipInterfaceImplementations,
                BinLogPath = testData.BinLogPath,
            };

            c.Docs.IncludedAssemblies.Add(assemblyName);

            if (!string.IsNullOrEmpty(namespaceName))
            {
                c.Docs.IncludedNamespaces.Add(namespaceName);
            }

            c.Docs.DirsDocsXml.Add(testData.DocsDir);

            CancellationTokenSource cts = new();

            VSLoader.LoadVSInstance();
            c.Loader = new MSBuildLoader(c.BinLogPath);

            await c.Loader.LoadMainProjectAsync(c.CsProj, c.IsMono, cts.Token);

            ToTripleSlashPorter porter = new(c);

            Log.Info("Iterating the docs xml files...");
            foreach (FileInfo fileInfo in porter.EnumerateDocsXmlFiles())
            {
                Log.Info($"Attempting to load xml file '{fileInfo.FullName}'...");
                DocsType? docsType = porter.LoadDocsTypeForFile(fileInfo);
                if (docsType == null)
                {
                    Log.Error($"Malformed file.");
                    continue;
                }

                Log.Success($"File loaded successfully.");
                Log.Info($"Looking for symbol locations for {docsType.TypeName}...");
                List<ResolvedLocation>? symbolLocations = await porter.CollectSymbolLocationsAsync(docsType.TypeName, cts.Token).ConfigureAwait(false);
                if (symbolLocations == null)
                {
                    Log.Error("No symbols found.");
                    continue;
                }
                Log.Info($"Finished looking for symbol locations for {docsType.TypeName}. Now attempting to port...");
                ResolvedDocsType rdt = new(docsType, symbolLocations);
                await porter.PortAsync(rdt, throwOnSymbolsNotFound: true, cts.Token).ConfigureAwait(false);
            }

            Verify(testData);
        }

        private static void Verify(TestData testData)
        {
            string[] expectedLines = File.ReadAllLines(testData.ExpectedFilePath);
            string[] actualLines = File.ReadAllLines(testData.ActualFilePath);

            for (int i = 0; i < expectedLines.Length; i++)
            {
                string expectedLine = expectedLines[i];
                string actualLine = actualLines[i];
                if (System.Diagnostics.Debugger.IsAttached)
                {
                    if (expectedLine != actualLine)
                    {
                        System.Diagnostics.Debugger.Break();
                    }
                }
                Assert.Equal(expectedLine, actualLine);
            }

            Assert.Equal(expectedLines.Length, actualLines.Length);
        }
    }
}

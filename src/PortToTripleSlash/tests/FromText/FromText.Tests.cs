// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using Xunit.Abstractions;

namespace ApiDocsSync.Libraries.Tests
{
    public class FromTextTests : BasePortTests
    {
        public FromTextTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void Test()
        {
            string tripleSlashText = "";
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(tripleSlashText);
        }
    }
}
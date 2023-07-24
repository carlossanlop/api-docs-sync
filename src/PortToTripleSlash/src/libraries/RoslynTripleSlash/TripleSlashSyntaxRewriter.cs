// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection.Emit;
using ApiDocsSync.PortToTripleSlash.Docs;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using System.Reflection.Metadata;
using System.Xml;
using System.Xml.Linq;

/*
 * According to the Roslyn Quoter: https://roslynquoter.azurewebsites.net/
 * This code:

/// <summary>Hello</summary>
/// <remarks>World</remarks>
public class MyClass { }

 * Can be generated using:

SyntaxFactory.CompilationUnit()
.WithMembers(
    SyntaxFactory.SingletonList<MemberDeclarationSyntax>(
        SyntaxFactory.ClassDeclaration("MyClass")
        .WithModifiers(
            SyntaxFactory.TokenList(
                SyntaxFactory.Token(
                    SyntaxFactory.TriviaList(
                        SyntaxFactory.Trivia(
                            SyntaxFactory.DocumentationCommentTrivia(
                                SyntaxKind.SingleLineDocumentationCommentTrivia,
                                SyntaxFactory.List<XmlNodeSyntax>(
                                    new XmlNodeSyntax[]{
                                        SyntaxFactory.XmlText()
                                        .WithTextTokens(
                                            SyntaxFactory.TokenList(
                                                SyntaxFactory.XmlTextLiteral(
                                                    SyntaxFactory.TriviaList(
                                                        SyntaxFactory.DocumentationCommentExterior("///")),
                                                    " ",
                                                    " ",
                                                    SyntaxFactory.TriviaList()))),
                                        SyntaxFactory.XmlExampleElement(
                                            SyntaxFactory.SingletonList<XmlNodeSyntax>(
                                                SyntaxFactory.XmlText()
                                                .WithTextTokens(
                                                    SyntaxFactory.TokenList(
                                                        SyntaxFactory.XmlTextLiteral(
                                                            SyntaxFactory.TriviaList(),
                                                            "Hello",
                                                            "Hello",
                                                            SyntaxFactory.TriviaList())))))
                                        .WithStartTag(
                                            SyntaxFactory.XmlElementStartTag(
                                                SyntaxFactory.XmlName(
                                                    SyntaxFactory.Identifier("summary"))))
                                        .WithEndTag(
                                            SyntaxFactory.XmlElementEndTag(
                                                SyntaxFactory.XmlName(
                                                    SyntaxFactory.Identifier("summary")))),
                                        SyntaxFactory.XmlText()
                                        .WithTextTokens(
                                            SyntaxFactory.TokenList(
                                                new []{
                                                    SyntaxFactory.XmlTextNewLine(
                                                        SyntaxFactory.TriviaList(),
                                                        "\n",
                                                        "\n",
                                                        SyntaxFactory.TriviaList()),
                                                    SyntaxFactory.XmlTextLiteral(
                                                        SyntaxFactory.TriviaList(
                                                            SyntaxFactory.DocumentationCommentExterior("///")),
                                                        " ",
                                                        " ",
                                                        SyntaxFactory.TriviaList())})),
                                        SyntaxFactory.XmlExampleElement(
                                            SyntaxFactory.SingletonList<XmlNodeSyntax>(
                                                SyntaxFactory.XmlText()
                                                .WithTextTokens(
                                                    SyntaxFactory.TokenList(
                                                        SyntaxFactory.XmlTextLiteral(
                                                            SyntaxFactory.TriviaList(),
                                                            "World",
                                                            "World",
                                                            SyntaxFactory.TriviaList())))))
                                        .WithStartTag(
                                            SyntaxFactory.XmlElementStartTag(
                                                SyntaxFactory.XmlName(
                                                    SyntaxFactory.Identifier("remarks"))))
                                        .WithEndTag(
                                            SyntaxFactory.XmlElementEndTag(
                                                SyntaxFactory.XmlName(
                                                    SyntaxFactory.Identifier("remarks")))),
                                        SyntaxFactory.XmlText()
                                        .WithTextTokens(
                                            SyntaxFactory.TokenList(
                                                SyntaxFactory.XmlTextNewLine(
                                                    SyntaxFactory.TriviaList(),
                                                    "\n",
                                                    "\n",
                                                    SyntaxFactory.TriviaList())))})))),
                    SyntaxKind.PublicKeyword,
                    SyntaxFactory.TriviaList())))))
.NormalizeWhitespace()
 
*/

namespace ApiDocsSync.PortToTripleSlash.Roslyn
{
    internal class TripleSlashSyntaxRewriter : CSharpSyntaxRewriter
    {
        private DocsCommentsContainer DocsComments { get; }
        private ResolvedLocation Location { get; }
        private SemanticModel Model => Location.Model;

        public TripleSlashSyntaxRewriter(DocsCommentsContainer docsComments, ResolvedLocation resolvedLocation) : base(visitIntoStructuredTrivia: false)
        {
            DocsComments = docsComments;
            Location = resolvedLocation;
        }

        // TYPE VISITORS

        public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node) => VisitType(node);

        public override SyntaxNode? VisitDelegateDeclaration(DelegateDeclarationSyntax node) => VisitType(node);

        public override SyntaxNode? VisitEnumDeclaration(EnumDeclarationSyntax node) => VisitType(node);

        public override SyntaxNode? VisitInterfaceDeclaration(InterfaceDeclarationSyntax node) => VisitType(node);

        public override SyntaxNode? VisitRecordDeclaration(RecordDeclarationSyntax node) => VisitType(node);

        public override SyntaxNode? VisitStructDeclaration(StructDeclarationSyntax node) => VisitType(node);

        // VARIABLE VISITORS

        public override SyntaxNode? VisitEventFieldDeclaration(EventFieldDeclarationSyntax node) => VisitVariableDeclaration(node);

        public override SyntaxNode? VisitFieldDeclaration(FieldDeclarationSyntax node) => VisitVariableDeclaration(node);

        // METHOD VISITORS

        public override SyntaxNode? VisitConstructorDeclaration(ConstructorDeclarationSyntax node) => VisitBaseMethodDeclaration(node);

        public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node) => VisitBaseMethodDeclaration(node);

        // TODO: Add test
        public override SyntaxNode? VisitConversionOperatorDeclaration(ConversionOperatorDeclarationSyntax node) => VisitBaseMethodDeclaration(node);

        // TODO: Add test
        public override SyntaxNode? VisitIndexerDeclaration(IndexerDeclarationSyntax node) => VisitBaseMethodDeclaration(node);

        public override SyntaxNode? VisitOperatorDeclaration(OperatorDeclarationSyntax node) => VisitBaseMethodDeclaration(node);

        // OTHER VISITORS

        public override SyntaxNode? VisitEnumMemberDeclaration(EnumMemberDeclarationSyntax node) => VisitMemberDeclaration(node);

        public override SyntaxNode? VisitPropertyDeclaration(PropertyDeclarationSyntax node) => VisitBasePropertyDeclaration(node);

        // THESE DO ACTUAL WORK

        private SyntaxNode? VisitType(SyntaxNode? node)
        {
            if (!TryGetType(node, out DocsType? type))
            {
                return node;
            }
            return Generate(node, type);
        }

        private SyntaxNode? VisitBaseMethodDeclaration(SyntaxNode? node)
        {
            // The Docs files only contain docs for public elements,
            // so if no comments are found, we return the node unmodified
            if (!TryGetMember(node, out DocsMember? member))
            {
                return node;
            }
            return Generate(node, member);
        }

        private SyntaxNode? VisitBasePropertyDeclaration(SyntaxNode? node)
        {
            if (!TryGetMember(node, out DocsMember? member))
            {
                return node;
            }
            return Generate(node, member);
        }

        // These nodes never have remarks
        private SyntaxNode? VisitMemberDeclaration(SyntaxNode? node)
        {
            if (!TryGetMember(node, out DocsMember? member))
            {
                return node;
            }
            return Generate(node, member);
        }

        private SyntaxNode? VisitVariableDeclaration(SyntaxNode? node)
        {
            if (node is not BaseFieldDeclarationSyntax baseFieldDeclaration)
            {
                return node;
            }

            // The comments need to be extracted from the underlying variable declarator inside the declaration
            VariableDeclarationSyntax declaration = baseFieldDeclaration.Declaration;

            // Only port docs if there is only one variable in the declaration
            if (declaration.Variables.Count == 1)
            {
                if (!TryGetMember(declaration.Variables.First(), out DocsMember? member))
                {
                    return node;
                }

                return Generate(node, member);
            }

            return node;
        }

        // API DOCS RETRIEVAL METHODS

        private bool TryGetMember([NotNullWhen(returnValue: true)] SyntaxNode? node, [NotNullWhen(returnValue: true)] out DocsMember? member)
        {
            member = null;

            if (!IsPublic(node))
            {
                return false;
            }

            if (Model.GetDeclaredSymbol(node) is ISymbol symbol)
            {
                string? docId = symbol.GetDocumentationCommentId();
                if (!string.IsNullOrWhiteSpace(docId))
                {
                    DocsComments.Members.TryGetValue(docId, out member);
                }
            }

            return member != null;
        }

        private bool TryGetType([NotNullWhen(returnValue: true)] SyntaxNode? node, [NotNullWhen(returnValue: true)] out DocsType? type)
        {
            type = null;

            if (node == null || !IsPublic(node))
            {
                return false;
            }

            if (Model.GetDeclaredSymbol(node) is ISymbol symbol)
            {
                string? docId = symbol.GetDocumentationCommentId();
                if (!string.IsNullOrWhiteSpace(docId))
                {
                    DocsComments.Types.TryGetValue(docId, out type);
                }
            }

            return type != null;
        }

        private static bool IsPublic([NotNullWhen(returnValue: true)] SyntaxNode? node)
        {
            if (node == null ||
                node is not MemberDeclarationSyntax baseNode ||
                !baseNode.Modifiers.Any(t => t.IsKind(SyntaxKind.PublicKeyword)))
            {
                return false;
            }

            return true;
        }

        public SyntaxNode Generate(SyntaxNode node, IDocsAPI api)
        {
            List<SyntaxTrivia> updatedLeadingTrivia = new();

            bool replacedExisting = false;
            SyntaxTriviaList leadingTrivia = node.GetLeadingTrivia();

            SyntaxTrivia? lastTrivia = leadingTrivia.Count > 0 ? leadingTrivia.Last(x => x.IsKind(SyntaxKind.WhitespaceTrivia)) : null;
            for (int index = 0; index < leadingTrivia.Count; index++)
            {
                SyntaxTrivia originalTrivia = leadingTrivia[index];

                if (index == leadingTrivia.Count - 1)
                {
                    break;
                }

                if (!originalTrivia.HasStructure)
                {
                    updatedLeadingTrivia.Add(originalTrivia);
                    continue;
                }

                SyntaxNode? structuredTrivia = originalTrivia.GetStructure();
                Debug.Assert(structuredTrivia != null);

                if (!structuredTrivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia))
                {
                    updatedLeadingTrivia.Add(originalTrivia);
                    continue;
                }

                DocumentationCommentTriviaSyntax documentationCommentTrivia = (DocumentationCommentTriviaSyntax)structuredTrivia;

                SyntaxList<SyntaxNode> updatedNodeList = GetUpdatedXmlElements(documentationCommentTrivia.Content, api, DocsComments.Config.SkipRemarks, lastTrivia.Value);
                if (updatedNodeList.Any())
                {
                    DocumentationCommentTriviaSyntax updatedDocComments = SyntaxFactory.DocumentationCommentTrivia(
                        SyntaxKind.SingleLineDocumentationCommentTrivia,
                        (SyntaxList<XmlNodeSyntax>)SyntaxFactory.List(updatedNodeList));

                    updatedDocComments = updatedDocComments
                        .WithLeadingTrivia(structuredTrivia.GetLeadingTrivia())
                        .WithTrailingTrivia(structuredTrivia.GetTrailingTrivia());

                    SyntaxTrivia updatedTrivia = SyntaxFactory.Trivia(updatedDocComments);
                    updatedLeadingTrivia.Add(updatedTrivia);
                    replacedExisting = false;
                }
                else
                {
                    SyntaxTrivia updatedTrivia = CreateXmlFromScratch(api, lastTrivia.Value);
                    updatedLeadingTrivia.Add(updatedTrivia);
                    replacedExisting = true;
                }
            }

            // Either there was no pre-existing trivia or there were no existing triple slash
            // So need to build it from scratch
            if (!replacedExisting)
            {
                SyntaxTrivia newTrivia = CreateXmlFromScratch(api, lastTrivia.Value);
                updatedLeadingTrivia.Add(newTrivia);
            }

            // The last trivia is the spacing before the actual node (usually before the visibility keyword)
            // must be replaced in its original location
            if (lastTrivia != null)
            {
                updatedLeadingTrivia.Add(lastTrivia.Value);
            }

            return node.WithLeadingTrivia(updatedLeadingTrivia);
        }

        private SyntaxTrivia CreateXmlFromScratch(IDocsAPI api, SyntaxTrivia lastTrivia)
        {
            // TODO: Add all the empty items needed for this API and wrap them in their expected greater items
            SyntaxList<SyntaxNode> newNodeList = GetUpdatedXmlElements(SyntaxFactory.List<XmlNodeSyntax>(), api, DocsComments.Config.SkipRemarks, lastTrivia);

            DocumentationCommentTriviaSyntax newDocComments = SyntaxFactory.DocumentationCommentTrivia(SyntaxKind.SingleLineDocumentationCommentTrivia, newNodeList);

            return SyntaxFactory.Trivia(newDocComments);
        }

        internal SyntaxList<SyntaxNode> GetUpdatedXmlElements(SyntaxList<XmlNodeSyntax> originalXmls, IDocsAPI api, bool skipRemarks, SyntaxTrivia lastTrivia)
        {
            List<SyntaxNode> updated = new();

            // Summary is in all api kinds
            List<XmlNodeSyntax> summaryNode = GetOrCreateXmlNode(originalXmls, "summary", api.Summary, lastTrivia, isFirst: true, isLast: false);
            updated.AddRange(summaryNode);

            // Summary can be in delegates (which are types) and is also in methods and properties
            if (api.ReturnType != string.Empty && api.ReturnType != "System.Void")
            {
                List<XmlNodeSyntax> returnsNode = GetOrCreateXmlNode(originalXmls, "returns", api.Returns, lastTrivia);
                updated.AddRange(returnsNode);
            }

            if (!skipRemarks)
            {
                List<XmlNodeSyntax> remarksNode = GetOrCreateXmlNode(originalXmls, "remarks", api.Remarks, lastTrivia, isLast: true);
                updated.AddRange(remarksNode);

            }

            return new SyntaxList<SyntaxNode>(updated);
        }

        private List<XmlNodeSyntax> GetOrCreateXmlNode(SyntaxList<XmlNodeSyntax> originalXmls, string tagName, string apiDocsText, SyntaxTrivia lastTrivia, bool isFirst = false, bool isLast = false)
        {
            SyntaxTokenList tokenList;
            if (apiDocsText.IsDocsEmpty())
            {
                // Override with api docs text
                tokenList = SyntaxFactory.TokenList(SyntaxFactory.ParseToken(apiDocsText));
            }
            else
            {

                // Try to get existing one, or if not found, create an empty one
                XmlNodeSyntax? xmlNode = originalXmls.FirstOrDefault(xmlNode => DoesNodeHasTag(xmlNode, tagName));

                if (xmlNode == null)
                {
                    tokenList = SyntaxFactory.TokenList();
                }
                else
                {
                    XmlElementSyntax xmlElement = (XmlElementSyntax)xmlNode;
                    XmlTextSyntax xmlText = (XmlTextSyntax)xmlElement.Content.Single();
                    tokenList = xmlText.TextTokens;
                }
            }

            List<XmlNodeSyntax> list = new();

            if (isFirst)
            {
                list.Add(GetPrefixTokens(lastTrivia));
            }

            list.Add(CreateXmlNode(tagName, tokenList));

            list.Add(GetSuffixTokens(lastTrivia, isLast));

            return list;
        }

        private SyntaxToken GetXmlTripleSlash(SyntaxTrivia lastTrivia)
        {
            return SyntaxFactory.XmlTextLiteral(
                        leading: SyntaxFactory.TriviaList(lastTrivia, SyntaxFactory.DocumentationCommentExterior("///")),
                        text: " ",
                        value: " ",
                        trailing: SyntaxFactory.TriviaList());
        }

        private XmlNodeSyntax GetPrefixTokens(SyntaxTrivia lastTrivia) => SyntaxFactory.XmlText().WithTextTokens(SyntaxFactory.TokenList(GetXmlTripleSlash(lastTrivia)));

        private XmlNodeSyntax GetSuffixTokens(SyntaxTrivia lastTrivia, bool isLast)
        {
            List<SyntaxToken> tokens = new()
            {
                SyntaxFactory.XmlTextNewLine(
                                    leading: SyntaxFactory.TriviaList(),
                                    text: "\n",
                                    value: "\n",
                                    trailing: SyntaxFactory.TriviaList())
            };
            if (!isLast)
            {
                tokens.Add(GetXmlTripleSlash(lastTrivia));
            }

            return SyntaxFactory.XmlText().WithTextTokens(SyntaxFactory.TokenList(tokens));
        }

        private XmlNodeSyntax CreateXmlNode(string tagName, SyntaxTokenList tokenList)
        {
            return SyntaxFactory.XmlExampleElement(
                        SyntaxFactory.SingletonList<XmlNodeSyntax>(
                            SyntaxFactory.XmlText()
                            .WithTextTokens(tokenList)))
                        .WithStartTag(
                        SyntaxFactory.XmlElementStartTag(
                            SyntaxFactory.XmlName(
                                SyntaxFactory.Identifier(tagName))))
                        .WithEndTag(
                        SyntaxFactory.XmlElementEndTag(
                            SyntaxFactory.XmlName(
                                SyntaxFactory.Identifier(tagName))));
        }

        private bool DoesNodeHasTag(SyntaxNode xmlNode, string tagName)
        {
            return xmlNode.Kind() is SyntaxKind.XmlElement &&
            xmlNode is XmlElementSyntax xmlElement &&
            xmlElement.StartTag.Name.LocalName.ValueText == tagName;
        }
    }
}

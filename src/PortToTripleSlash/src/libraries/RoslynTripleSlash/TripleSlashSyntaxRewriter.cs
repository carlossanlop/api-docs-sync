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

namespace ApiDocsSync.PortToTripleSlash.Roslyn
{
    /*
    The following triple slash comments section:

        /// <summary>
        /// My summary.
        /// </summary>
        /// <param name="paramName">My param description.</param>
        /// <remarks>My remarks.</remarks>
        public ...

    translates to this syntax tree structure:

    PublicKeyword (SyntaxToken) -> The public keyword including its trivia.
        Lead: EndOfLineTrivia -> The newline char before the 4 whitespace chars before the triple slash comments.
        Lead: WhitespaceTrivia -> The 4 whitespace chars before the triple slash comments.
        Lead: SingleLineDocumentationCommentTrivia (SyntaxTrivia)
            SingleLineDocumentationCommentTrivia (DocumentationCommentTriviaSyntax) -> The triple slash comments, excluding the first 3 slash chars.
                XmlText (XmlTextSyntax)
                    XmlTextLiteralToken (SyntaxToken) -> The space between the first triple slash and <summary>.
                        Lead: DocumentationCommentExteriorTrivia (SyntaxTrivia) -> The first 3 slash chars.

                XmlElement (XmlElementSyntax) -> From <summary> to </summary>. Excludes the first 3 slash chars, but includes the second and third trios.
                    XmlElementStartTag (XmlElementStartTagSyntax) -> <summary>
                        LessThanToken (SyntaxToken) -> <
                        XmlName (XmlNameSyntax) -> summary
                            IdentifierToken (SyntaxToken) -> summary
                        GreaterThanToken (SyntaxToken) -> >
                    XmlText (XmlTextSyntax) -> Everything after <summary> and before </summary>
                        XmlTextLiteralNewLineToken (SyntaxToken) -> endline after <summary>
                        XmlTextLiteralToken (SyntaxToken) -> [ My summary.]
                            Lead: DocumentationCommentExteriorTrivia (SyntaxTrivia) -> endline after summary text
                        XmlTextLiteralNewToken (SyntaxToken) -> Space between 3 slashes and </summary>
                            Lead: DocumentationCommentExteriorTrivia (SyntaxTrivia) -> whitespace + 3 slashes before the </summary>
                    XmlElementEndTag (XmlElementEndTagSyntax) -> </summary>
                        LessThanSlashToken (SyntaxToken) -> </
                        XmlName (XmlNameSyntax) -> summary
                            IdentifierToken (SyntaxToken) -> summary
                        GreaterThanToken (SyntaxToken) -> >
                XmlText -> endline + whitespace + 3 slahes before <param
                    XmlTextLiteralNewLineToken (XmlTextSyntax) -> endline after </summary>
                    XmlTextLiteralToken (XmlTextLiteralToken) -> space after 3 slashes and before <param
                        Lead: DocumentationCommentExteriorTrivia (SyntaxTrivia) -> whitespace + 3 slashes before the space and <param

                XmlElement -> <param name="...">...</param>
                    XmlElementStartTag -> <param name="...">
                        LessThanToken -> <
                        XmlName -> param
                            IdentifierToken -> param
                        XmlNameAttribute (XmlNameAttributeSyntax) -> name="paramName"
                            XmlName -> name
                                IdentifierToken -> name
                                    Lead: WhitespaceTrivia -> space between param and name
                            EqualsToken -> =
                            DoubleQuoteToken -> opening "
                            IdentifierName -> paramName
                                IdentifierToken -> paramName
                            DoubleQuoteToken -> closing "
                        GreaterThanToken -> >
                    XmlText -> My param description.
                        XmlTextLiteralToken -> My param description.
                    XmlElementEndTag -> </param>
                        LessThanSlashToken -> </
                        XmlName -> param
                            IdentifierToken -> param
                        GreaterThanToken -> >
                XmlText -> newline + 4 whitespace chars + /// before <remarks>

                XmlElement -> <remarks>My remarks.</remarks>
                XmlText -> new line char after </remarks>
                    XmlTextLiteralNewLineToken -> new line char after </remarks>
                EndOfDocumentationCommentToken (SyntaxToken) -> invisible

        Lead: WhitespaceTrivia -> The 4 whitespace chars before the public keyword.
        Trail: WhitespaceTrivia -> The single whitespace char after the public keyword.
    */
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
            foreach (SyntaxTrivia trivia in node.GetLeadingTrivia())
            {
                if (!trivia.HasStructure)
                {
                    updatedLeadingTrivia.Add(trivia);
                    continue;
                }

                SyntaxNode? structuredTrivia = trivia.GetStructure();
                Debug.Assert(structuredTrivia != null);

                if (!structuredTrivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia))
                {
                    updatedLeadingTrivia.Add(trivia);
                    continue;
                }

                if (structuredTrivia is DocumentationCommentTriviaSyntax documentationCommentTrivia)
                {
                    List<SyntaxNode> updatedNodeList = GetUpdatedXmlElements(documentationCommentTrivia.Content, api, DocsComments.Config.SkipRemarks);

                    DocumentationCommentTriviaSyntax newDocComments = DocumentationCommentTriviaWithUpdatedContent(
                        documentationCommentTrivia.Kind(), updatedNodeList, documentationCommentTrivia.EndOfComment);

                    newDocComments = newDocComments
                        .WithLeadingTrivia(structuredTrivia.GetLeadingTrivia())
                        .WithTrailingTrivia(structuredTrivia.GetTrailingTrivia());

                    SyntaxTrivia newTrivia = SyntaxFactory.Trivia(newDocComments);
                    updatedLeadingTrivia.Add(newTrivia);

                    replacedExisting = true;
                }
                else
                {
                    throw new NotSupportedException($"Unsupported trivia kind: {trivia.Kind()}");
                }
            }

            // Either there was no pre-existing trivia or there were no existing triple slash
            // So need to build it from scratch
            if (!replacedExisting)
            {
            }

            return node.WithLeadingTrivia(updatedLeadingTrivia);
        }

        // This exists in SyntaxGenerator in dotnet/roslyn but it's not accessible.
        private DocumentationCommentTriviaSyntax DocumentationCommentTriviaWithUpdatedContent(SyntaxKind documentationCommentTriviaKind, IEnumerable<SyntaxNode> content, SyntaxToken documentationCommentTriviaEndOfComment)
        {
            return SyntaxFactory.DocumentationCommentTrivia(
                documentationCommentTriviaKind,
                (SyntaxList<XmlNodeSyntax>)SyntaxFactory.List(content),
                documentationCommentTriviaEndOfComment);
        }


        internal List<SyntaxNode> GetUpdatedXmlElements(SyntaxList<XmlNodeSyntax> originalXmls, IDocsAPI api, bool skipRemarks)
        {
            List<SyntaxNode> updated = new();

            // Summary is in all api kinds
            XmlNodeSyntax summaryNode = GetOrCreateXmlNode(originalXmls, "summary", api.Summary);
            updated.Add(summaryNode);

            if (api.ReturnType is not "" and not "System.Void")
            {
                XmlNodeSyntax returnsNode = GetOrCreateXmlNode(originalXmls, "returns", api.Returns);
                updated.Add(returnsNode);
            }

            if (!skipRemarks)
            {
                XmlNodeSyntax remarksNode = GetOrCreateXmlNode(originalXmls, "remarks", api.Remarks);
                updated.Add(remarksNode);
            }

            return updated;

            // Find summary xml among existing triple slash, if found
            //    check if you have a Summary string. If you do, override the found summary xml text
            //    then re-add the (maybe updated) summary xml
            // if not found, add an empty one.

            // Then depending on the API type, do the same for the other items in the expected order

            // For optional items, backport them only if they are relevant to the developer
            // Say you want to backport relateds (maybe not relevant), so see if you have a related xml among existing triple slash, if found
            //    check if you have a Related string. If you do, override the found related xml text
            //    if not found, add a new related xml text with the Related string
            // then add (or re-add) the related xml

            // Do the same with remarks as you did with summary but only if Config says you can

            //SyntaxTrivia trivia = node.GetLeadingTrivia().SingleOrDefault(t => t.HasStructure);

            //List<XmlNodeSyntax> content = new();

            //SyntaxToken summaryLiteral = SyntaxFactory.XmlTextLiteral("I am the summary");
            //XmlTextSyntax summaryText = SyntaxFactory.XmlText(summaryLiteral);
            //XmlElementSyntax summary = SyntaxFactory.XmlSummaryElement(summaryText);
            //content.Add(summary);

            //SyntaxToken remarksLiteral = SyntaxFactory.XmlTextLiteral("I am the remarks");
            //XmlTextSyntax remarksText = SyntaxFactory.XmlText(remarksLiteral);
            //XmlElementSyntax remarks = SyntaxFactory.XmlRemarksElement(remarksText);
            //content.Add(remarks);

            //return newNode;
        }

        private XmlNodeSyntax GetOrCreateXmlNode(SyntaxList<XmlNodeSyntax> originalXmls, string tagName, string apiDocsText)
        {
            if (!apiDocsText.IsDocsEmpty())
            {
                // Override with api docs text
                return CreateXmlNode(tagName, apiDocsText);
            }

            // Try to get existing one, or if not found, create an empty one
            return originalXmls.FirstOrDefault(xmlNode => DoesNodeHasTag(xmlNode, tagName)) ??
                   CreateXmlNode(tagName, string.Empty);
        }

        private XmlNodeSyntax CreateXmlNode(string tagName, string text)
        {
            return SyntaxFactory.XmlExampleElement(
                        SyntaxFactory.SingletonList<XmlNodeSyntax>(
                            SyntaxFactory.XmlText()
                            .WithTextTokens(
                                SyntaxFactory.TokenList(
                                    SyntaxFactory.XmlTextLiteral(
                                        SyntaxFactory.TriviaList(),
                                        text,
                                        text,
                                        SyntaxFactory.TriviaList())))))
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
            xmlElement.StartTag.Name is XmlNameSyntax xmlName &&
            xmlName.LocalName.ValueText == tagName;
        }
    }
}

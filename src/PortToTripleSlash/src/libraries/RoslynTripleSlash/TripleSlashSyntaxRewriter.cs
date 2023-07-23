// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ApiDocsSync.PortToTripleSlash.Docs;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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
            if (node == null || !TryGetType(node, out DocsType? type))
            {
                return node;
            }
            
            return TriviaGenerator.Generate(DocsComments.Config, Model, node, type);
        }

        private SyntaxNode? VisitBaseMethodDeclaration(SyntaxNode? node)
        {
            // The Docs files only contain docs for public elements,
            // so if no comments are found, we return the node unmodified
            if (node == null || !TryGetMember(node, out DocsMember? member))
            {
                return node;
            }
            
            return TriviaGenerator.Generate(DocsComments.Config, Model, node, member);
        }

        private SyntaxNode? VisitBasePropertyDeclaration(SyntaxNode? node)
        {
            if (!TryGetMember(node, out DocsMember? member))
            {
                return node;
            }
            return TriviaGenerator.Generate(DocsComments.Config, Model, node, member);
        }

        // These nodes never have remarks
        private SyntaxNode? VisitMemberDeclaration(SyntaxNode? node)
        {
            if (!TryGetMember(node, out DocsMember? member))
            {
                return node;
            }
            return TriviaGenerator.Generate(DocsComments.Config, Model, node, member);
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
                return TriviaGenerator.Generate(DocsComments.Config, Model, node, member);
            }

            return node;
        }

        // API DOCS RETRIEVAL METHODS

        private bool TryGetMember(SyntaxNode? node, [NotNullWhen(returnValue: true)] out DocsMember? member)
        {
            member = null;

            if (node == null || !IsPublic(node))
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

        private bool TryGetType(SyntaxNode? node, [NotNullWhen(returnValue: true)] out DocsType? type)
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

        private bool IsPublic(SyntaxNode? node)
        {
            if (node is not MemberDeclarationSyntax baseNode ||
                !baseNode.Modifiers.Any(t => t.IsKind(SyntaxKind.PublicKeyword)))
            {
                return false;
            }

            return true;
        }
    }
}

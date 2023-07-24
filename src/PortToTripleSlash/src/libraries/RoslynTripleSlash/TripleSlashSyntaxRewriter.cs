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

public class MyClass
{
    /// <summary>MySummary</summary>
    /// <param name="x">MyParameter</param>
    public void MyMethod(int x) { }
}

 * Can be generated using:

SyntaxFactory.CompilationUnit()
.WithMembers(
    SyntaxFactory.SingletonList<MemberDeclarationSyntax>(
        SyntaxFactory.ClassDeclaration("MyClass")
        .WithModifiers(
            SyntaxFactory.TokenList(
                SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
        .WithMembers(
            SyntaxFactory.SingletonList<MemberDeclarationSyntax>(
                SyntaxFactory.MethodDeclaration(
                    SyntaxFactory.PredefinedType(
                        SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
                    SyntaxFactory.Identifier("MyMethod"))
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
                                                                    "MySummary",
                                                                    "MySummary",
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
                                                                    SyntaxFactory.DocumentationCommentExterior("    ///")),
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
                                                                    "MyParameter",
                                                                    "MyParameter",
                                                                    SyntaxFactory.TriviaList())))))
                                                .WithStartTag(
                                                    SyntaxFactory.XmlElementStartTag(
                                                        SyntaxFactory.XmlName(
                                                            SyntaxFactory.Identifier(
                                                                SyntaxFactory.TriviaList(),
                                                                SyntaxKind.ParamKeyword,
                                                                "param",
                                                                "param",
                                                                SyntaxFactory.TriviaList())))
                                                    .WithAttributes(
                                                        SyntaxFactory.SingletonList<XmlAttributeSyntax>(
                                                            SyntaxFactory.XmlNameAttribute(
                                                                SyntaxFactory.XmlName(
                                                                    SyntaxFactory.Identifier("name")),
                                                                SyntaxFactory.Token(SyntaxKind.DoubleQuoteToken),
                                                                SyntaxFactory.IdentifierName("x"),
                                                                SyntaxFactory.Token(SyntaxKind.DoubleQuoteToken)))))
                                                .WithEndTag(
                                                    SyntaxFactory.XmlElementEndTag(
                                                        SyntaxFactory.XmlName(
                                                            SyntaxFactory.Identifier(
                                                                SyntaxFactory.TriviaList(),
                                                                SyntaxKind.ParamKeyword,
                                                                "param",
                                                                "param",
                                                                SyntaxFactory.TriviaList())))),
                                                SyntaxFactory.XmlText()
                                                .WithTextTokens(
                                                    SyntaxFactory.TokenList(
                                                        SyntaxFactory.XmlTextNewLine(
                                                            SyntaxFactory.TriviaList(),
                                                            "\n",
                                                            "\n",
                                                            SyntaxFactory.TriviaList())))})))),
                            SyntaxKind.PublicKeyword,
                            SyntaxFactory.TriviaList())))
                .WithParameterList(
                    SyntaxFactory.ParameterList(
                        SyntaxFactory.SingletonSeparatedList<ParameterSyntax>(
                            SyntaxFactory.Parameter(
                                SyntaxFactory.Identifier("x"))
                            .WithType(
                                SyntaxFactory.PredefinedType(
                                    SyntaxFactory.Token(SyntaxKind.IntKeyword))))))
                .WithBody(
                    SyntaxFactory.Block())))))
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

        public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node) => VisitType(node, base.VisitClassDeclaration(node));

        public override SyntaxNode? VisitDelegateDeclaration(DelegateDeclarationSyntax node) => VisitType(node, base.VisitDelegateDeclaration(node));
        private SyntaxNode? VisitType(DelegateDeclarationSyntax node) => throw new NotImplementedException();
        public override SyntaxNode? VisitEnumDeclaration(EnumDeclarationSyntax node) => VisitType(node, base.VisitEnumDeclaration(node));

        public override SyntaxNode? VisitInterfaceDeclaration(InterfaceDeclarationSyntax node) => VisitType(node, base.VisitInterfaceDeclaration(node));

        public override SyntaxNode? VisitRecordDeclaration(RecordDeclarationSyntax node) => VisitType(node, base.VisitRecordDeclaration(node));

        public override SyntaxNode? VisitStructDeclaration(StructDeclarationSyntax node) => VisitType(node, base.VisitStructDeclaration(node));

        // VARIABLE VISITORS

        public override SyntaxNode? VisitEventFieldDeclaration(EventFieldDeclarationSyntax node) => VisitVariableDeclaration(node, base.VisitEventFieldDeclaration(node));

        public override SyntaxNode? VisitFieldDeclaration(FieldDeclarationSyntax node) => VisitVariableDeclaration(node, base.VisitFieldDeclaration(node));

        // METHOD VISITORS

        public override SyntaxNode? VisitConstructorDeclaration(ConstructorDeclarationSyntax node) => VisitBaseMethodDeclaration(node, base.VisitConstructorDeclaration(node));

        public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node) => VisitBaseMethodDeclaration(node, base.VisitMethodDeclaration(node));

        // TODO: Add test
        public override SyntaxNode? VisitConversionOperatorDeclaration(ConversionOperatorDeclarationSyntax node) => VisitBaseMethodDeclaration(node, base.VisitConversionOperatorDeclaration(node));

        // TODO: Add test
        public override SyntaxNode? VisitIndexerDeclaration(IndexerDeclarationSyntax node) => VisitBaseMethodDeclaration(node, base.VisitIndexerDeclaration(node));

        public override SyntaxNode? VisitOperatorDeclaration(OperatorDeclarationSyntax node) => VisitBaseMethodDeclaration(node, base.VisitOperatorDeclaration(node));

        // OTHER VISITORS

        public override SyntaxNode? VisitEnumMemberDeclaration(EnumMemberDeclarationSyntax node) => VisitMemberDeclaration(node, base.VisitEnumMemberDeclaration(node));

        public override SyntaxNode? VisitPropertyDeclaration(PropertyDeclarationSyntax node) => VisitBasePropertyDeclaration(node, base.VisitPropertyDeclaration(node));

        // THESE DO ACTUAL WORK

        private SyntaxNode? VisitType(SyntaxNode originalNode, SyntaxNode? baseNode)
        {
            if (!TryGetType(originalNode, out DocsType? type) || baseNode == null)
            {
                return originalNode;
            }
            return Generate(baseNode, type);
        }

        private SyntaxNode? VisitBaseMethodDeclaration(SyntaxNode originalNode, SyntaxNode? baseNode)
        {
            // The Docs files only contain docs for public elements,
            // so if no comments are found, we return the node unmodified
            if (!TryGetMember(originalNode, out DocsMember? member) || baseNode == null)
            {
                return originalNode;
            }
            return Generate(baseNode, member);
        }

        private SyntaxNode? VisitBasePropertyDeclaration(SyntaxNode originalNode, SyntaxNode? baseNode)
        {
            if (!TryGetMember(originalNode, out DocsMember? member) || baseNode == null)
            {
                return originalNode;
            }
            return Generate(baseNode, member);
        }

        // These nodes never have remarks
        private SyntaxNode? VisitMemberDeclaration(SyntaxNode originalNode, SyntaxNode? baseNode)
        {
            if (!TryGetMember(originalNode, out DocsMember? member) || baseNode == null)
            {
                return originalNode;
            }
            return Generate(baseNode, member);
        }

        private SyntaxNode? VisitVariableDeclaration(SyntaxNode originalNode, SyntaxNode? baseNode)
        {
            if (baseNode == null || originalNode is not BaseFieldDeclarationSyntax originalFieldDeclaration)
            {
                return originalNode;
            }

            // The comments need to be extracted from the underlying variable declarator inside the declaration
            VariableDeclarationSyntax declaration = originalFieldDeclaration.Declaration;

            // Only port docs if there is only one variable in the declaration
            if (declaration.Variables.Count == 1)
            {
                if (!TryGetMember(declaration.Variables.First(), out DocsMember? member))
                {
                    return baseNode;
                }

                return Generate(baseNode, member);
            }

            return baseNode;
        }

        // API DOCS RETRIEVAL METHODS

        private bool TryGetMember(SyntaxNode originalNode, [NotNullWhen(returnValue: true)] out DocsMember? member)
        {
            member = null;

            if (!IsPublic(originalNode))
            {
                return false;
            }

            if (Model.GetDeclaredSymbol(originalNode) is ISymbol symbol)
            {
                string? docId = symbol.GetDocumentationCommentId();
                if (!string.IsNullOrWhiteSpace(docId))
                {
                    DocsComments.Members.TryGetValue(docId, out member);
                }
            }

            return member != null;
        }

        private bool TryGetType(SyntaxNode originalNode, [NotNullWhen(returnValue: true)] out DocsType? type)
        {
            type = null;

            if (originalNode == null || !IsPublic(originalNode))
            {
                return false;
            }

            if (Model.GetDeclaredSymbol(originalNode) is ISymbol symbol)
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

                SyntaxList<SyntaxNode> updatedNodeList = GetUpdatedXmlElements(documentationCommentTrivia.Content, api, DocsComments.Config.SkipRemarks, lastTrivia);
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
                }
                else
                {
                    SyntaxTrivia updatedTrivia = CreateXmlFromScratch(api, lastTrivia);
                    updatedLeadingTrivia.Add(updatedTrivia);
                }
                replacedExisting = true;
            }

            // Either there was no pre-existing trivia or there were no existing triple slash
            // So need to build it from scratch
            if (!replacedExisting)
            {
                SyntaxTrivia newTrivia = CreateXmlFromScratch(api, lastTrivia);
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

        private SyntaxTrivia CreateXmlFromScratch(IDocsAPI api, SyntaxTrivia? lastTrivia)
        {
            // TODO: Add all the empty items needed for this API and wrap them in their expected greater items
            SyntaxList<SyntaxNode> newNodeList = GetUpdatedXmlElements(SyntaxFactory.List<XmlNodeSyntax>(), api, DocsComments.Config.SkipRemarks, lastTrivia);

            DocumentationCommentTriviaSyntax newDocComments = SyntaxFactory.DocumentationCommentTrivia(SyntaxKind.SingleLineDocumentationCommentTrivia, newNodeList);

            return SyntaxFactory.Trivia(newDocComments);
        }

        private const string SummaryStr = "summary";
        private const string ValueStr = "value";
        private const string TypeParamStr = "typeparam";
        private const string ParamStr = "param";
        private const string ReturnsStr = "returns";
        private const string RemarksStr = "remarks";
        private const string ExceptionStr = "exception";
        private const string SystemVoid = "System.Void";
        private const string NameStr = "name";
        private const string CrefStr = "cref";
        internal SyntaxList<SyntaxNode> GetUpdatedXmlElements(SyntaxList<XmlNodeSyntax> originalXmls, IDocsAPI api, bool skipRemarks, SyntaxTrivia? lastTrivia)
        {
            List<SyntaxNode> updated = new();

            // Summary is in all api kinds
            TryGetOrCreateXmlNode(originalXmls, updated, SummaryStr, api.Summary, lastTrivia, isFirst: true);

            // Value is always second and only shows up in properties
            TryGetOrCreateXmlNode(originalXmls, updated, ValueStr, api.Value, lastTrivia);

            foreach (DocsTypeParam typeParam in api.TypeParams)
            {
                TryGetOrCreateXmlNode(originalXmls, updated, TypeParamStr, typeParam.Value, lastTrivia, attributeName: NameStr, attributeValue: typeParam.Name);
            }

            foreach (DocsParam param in api.Params)
            {
                TryGetOrCreateXmlNode(originalXmls, updated, ParamStr, param.Value, lastTrivia, attributeName: NameStr, attributeValue: param.Name);
            }

            // Returns can be in delegates (which are types) and is also in methods and properties
            if (api.ReturnType != string.Empty && api.ReturnType != SystemVoid)
            {
                TryGetOrCreateXmlNode(originalXmls, updated, ReturnsStr, api.Returns, lastTrivia);
            }

            foreach (DocsException exception in api.Exceptions)
            {
                TryGetOrCreateXmlNode(originalXmls, updated, ExceptionStr, exception.Value, lastTrivia, attributeName: CrefStr, attributeValue: exception.Cref);
            }

            if (!skipRemarks)
            {
                TryGetOrCreateXmlNode(originalXmls, updated, RemarksStr, api.Remarks, lastTrivia, isLast: true);
            }

            return new SyntaxList<SyntaxNode>(updated);
        }

        private bool TryGetOrCreateXmlNode(SyntaxList<XmlNodeSyntax> originalXmls, List<SyntaxNode> updatedXmls,
            string tagName, string apiDocsText, SyntaxTrivia? lastTrivia, string? attributeName = null, string? attributeValue = null, bool isFirst = false, bool isLast = false)
        {
            SyntaxTokenList textLiteralTokenList;
            if (!apiDocsText.IsDocsEmpty())
            {
                // Overwrite the current triple slash with the text that comes from api docs
                SyntaxToken textLiteral = SyntaxFactory.XmlTextLiteral(leading: SyntaxFactory.TriviaList(), text: apiDocsText, value: apiDocsText, trailing: SyntaxFactory.TriviaList());
                textLiteralTokenList = SyntaxFactory.TokenList(textLiteral);
            }
            else
            {
                // Not yet documented in api docs, so try to see if it was documented in triple slash
                XmlNodeSyntax? xmlNode = originalXmls.FirstOrDefault(xmlNode => DoesNodeHasTag(xmlNode, tagName));

                if (xmlNode != null)
                {
                    XmlElementSyntax xmlElement = (XmlElementSyntax)xmlNode;
                    XmlTextSyntax xmlText = (XmlTextSyntax)xmlElement.Content.Single();
                    textLiteralTokenList = xmlText.TextTokens;
                }
                else
                {
                    // We don't want to add an empty xml item. We want it to be missing so the developer sees the compilation error and adds it.
                    return false;
                }
            }

            List<XmlNodeSyntax> xmlNodeList = new();

            if (isFirst)
            {
                xmlNodeList.Add(GetPrefixTokens(lastTrivia));
            }
            xmlNodeList.Add(CreateXmlNode(tagName, textLiteralTokenList, attributeName, attributeValue));
            xmlNodeList.Add(GetSuffixTokens(lastTrivia, isLast));

            updatedXmls.AddRange(xmlNodeList);

            return true;
        }

        private const string TripleSlash = "///";
        private const string Space = " ";
        private SyntaxToken GetXmlTripleSlash(SyntaxTrivia? lastTrivia)
        {
            List<SyntaxTrivia> triviaList = new();
            if (lastTrivia != null)
            {
                triviaList.Add(lastTrivia.Value);
            }
            triviaList.Add(SyntaxFactory.DocumentationCommentExterior(TripleSlash));

            return SyntaxFactory.XmlTextLiteral(
                        leading: SyntaxFactory.TriviaList(triviaList),
                        text: Space,
                        value: Space,
                        trailing: SyntaxFactory.TriviaList());
        }

        private XmlNodeSyntax GetPrefixTokens(SyntaxTrivia? lastTrivia) => SyntaxFactory.XmlText().WithTextTokens(SyntaxFactory.TokenList(GetXmlTripleSlash(lastTrivia)));

        private const string NewLine = "\n";
        private XmlNodeSyntax GetSuffixTokens(SyntaxTrivia? lastTrivia, bool isLast)
        {
            List<SyntaxToken> tokens = new()
            {
                SyntaxFactory.XmlTextNewLine(
                                    leading: SyntaxFactory.TriviaList(),
                                    text: NewLine,
                                    value: NewLine,
                                    trailing: SyntaxFactory.TriviaList())
            };
            if (!isLast)
            {
                tokens.Add(GetXmlTripleSlash(lastTrivia));
            }

            return SyntaxFactory.XmlText().WithTextTokens(SyntaxFactory.TokenList(tokens));
        }

        private XmlNodeSyntax CreateXmlNode(string tagName, SyntaxTokenList tokenList, string? attributeName = null, string? attributeValue = null)
        {
            SyntaxList<XmlNodeSyntax> content = SyntaxFactory.SingletonList<XmlNodeSyntax>(SyntaxFactory.XmlText().WithTextTokens(tokenList));

            XmlElementStartTagSyntax startTag = SyntaxFactory.XmlElementStartTag(SyntaxFactory.XmlName(SyntaxFactory.Identifier(tagName)));

            if (!string.IsNullOrWhiteSpace(attributeName) && !string.IsNullOrWhiteSpace(attributeValue))
            {
                XmlNameAttributeSyntax xmlAttribute = SyntaxFactory.XmlNameAttribute(SyntaxFactory.XmlName(SyntaxFactory.Identifier(attributeName)),
                                                                  SyntaxFactory.Token(SyntaxKind.DoubleQuoteToken),
                                                                  SyntaxFactory.IdentifierName(attributeValue),
                                                                  SyntaxFactory.Token(SyntaxKind.DoubleQuoteToken));
                SyntaxList<XmlAttributeSyntax> startTagAttributes = SyntaxFactory.SingletonList<XmlAttributeSyntax>(xmlAttribute);

                startTag = startTag.WithAttributes(startTagAttributes);
            }

            XmlElementEndTagSyntax endTag = SyntaxFactory.XmlElementEndTag(SyntaxFactory.XmlName(SyntaxFactory.Identifier(tagName)));

            return SyntaxFactory.XmlExampleElement(content)
                        .WithStartTag(startTag)
                        .WithEndTag(endTag);
        }

        private bool DoesNodeHasTag(SyntaxNode xmlNode, string tagName)
        {
            if (tagName == ExceptionStr)
            {
                // Temporary workaround to avoid overwriting all existing triple slash exceptions
                return false;
            }
            return xmlNode.Kind() is SyntaxKind.XmlElement &&
            xmlNode is XmlElementSyntax xmlElement &&
            xmlElement.StartTag.Name.LocalName.ValueText == tagName;
        }
    }
}

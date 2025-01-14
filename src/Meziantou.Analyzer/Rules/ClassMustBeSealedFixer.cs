﻿using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;

namespace Meziantou.Analyzer.Rules
{
    [ExportCodeFixProvider(LanguageNames.CSharp), Shared]
    public sealed class ClassMustBeSealedFixer : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(RuleIdentifiers.ClassMustBeSealed);

        public override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var nodeToFix = root.FindNode(context.Span, getInnermostNodeForTie: true) as ClassDeclarationSyntax;
            if (nodeToFix == null)
                return;

            var title = "Add sealed modifier";
            var codeAction = CodeAction.Create(
                title,
                ct => AddSealedModifier(context.Document, nodeToFix, ct),
                equivalenceKey: title);

            context.RegisterCodeFix(codeAction, context.Diagnostics);
        }

        private static async Task<Document> AddSealedModifier(Document document, SyntaxNode nodeToFix, CancellationToken cancellationToken)
        {
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

            var classNode = (ClassDeclarationSyntax)nodeToFix;
            editor.ReplaceNode(classNode, classNode.AddModifiers(SyntaxFactory.Token(SyntaxKind.SealedKeyword)).WithAdditionalAnnotations(Formatter.Annotation));
            return editor.GetChangedDocument();
        }

    }
}

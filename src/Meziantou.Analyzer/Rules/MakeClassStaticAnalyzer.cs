﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Meziantou.Analyzer.Rules
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class MakeClassStaticAnalyzer : DiagnosticAnalyzer
    {
        private static readonly DiagnosticDescriptor s_rule = new DiagnosticDescriptor(
            RuleIdentifiers.MakeClassStatic,
            title: "Make class static",
            messageFormat: "Make class static",
            RuleCategories.Design,
            DiagnosticSeverity.Info,
            isEnabledByDefault: true,
            description: "",
            helpLinkUri: RuleIdentifiers.GetHelpUri(RuleIdentifiers.AbstractTypesShouldNotHaveConstructors));

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(s_rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze);

            context.RegisterCompilationStartAction(ctx =>
            {
                var analyzerContext = new AnalyzerContext(ctx.Compilation);

                ctx.RegisterSymbolAction(analyzerContext.AnalyzeNamedTypeSymbol, SymbolKind.NamedType);
                ctx.RegisterOperationAction(analyzerContext.AnalyzeObjectCreation, OperationKind.ObjectCreation);
                ctx.RegisterOperationAction(analyzerContext.AnalyzeInvocation, OperationKind.Invocation);
                ctx.RegisterOperationAction(analyzerContext.AnalyzeArrayCreation, OperationKind.ArrayCreation);
                ctx.RegisterCompilationEndAction(analyzerContext.AnalyzeCompilationEnd);
            });
        }

        private static bool IsPotentialStatic(INamedTypeSymbol symbol)
        {
            return !symbol.IsAbstract &&
                !symbol.IsStatic &&
                !symbol.Interfaces.Any() &&
                !HasBaseClass() &&
                !symbol.IsUnitTestClass() &&
                symbol.GetMembers().All(member => (member.IsStatic || member.IsImplicitlyDeclared) && !member.IsOperator());

            bool HasBaseClass()
            {
                return symbol.BaseType != null && symbol.BaseType.SpecialType != SpecialType.System_Object;
            }
        }

        private sealed class AnalyzerContext
        {
            private readonly List<ITypeSymbol> _potentialClasses = new List<ITypeSymbol>();
            private readonly HashSet<ITypeSymbol> _cannotBeStaticClasses = new HashSet<ITypeSymbol>();

            public AnalyzerContext(Compilation compilation)
            {
                CoClassAttributeSymbol = compilation.GetTypeByMetadataName("System.Runtime.InteropServices.CoClassAttribute");
            }

            public INamedTypeSymbol CoClassAttributeSymbol { get; }

            public void AnalyzeNamedTypeSymbol(SymbolAnalysisContext context)
            {
                var symbol = (INamedTypeSymbol)context.Symbol;
                switch (symbol.TypeKind)
                {
                    case TypeKind.Class:
                        if (IsPotentialStatic(symbol))
                        {
                            lock (_potentialClasses)
                            {
                                _potentialClasses.Add(symbol);
                            }
                        }

                        if (symbol.BaseType != null)
                        {
                            AddCannotBeStaticType(symbol.BaseType);
                        }

                        break;

                    case TypeKind.Interface:
                        foreach (var attribute in symbol.GetAttributes().Where(attr => attr.AttributeClass.IsEqualTo(CoClassAttributeSymbol)))
                        {
                            var attributeValue = attribute.ConstructorArguments.FirstOrDefault();
                            if (!attributeValue.IsNull && attributeValue.Kind == TypedConstantKind.Type && attributeValue.Value is ITypeSymbol type)
                            {
                                AddCannotBeStaticType(type);
                            }
                        }

                        break;
                }
            }

            public void AnalyzeObjectCreation(OperationAnalysisContext context)
            {
                var operation = (IObjectCreationOperation)context.Operation;
                AddCannotBeStaticType(operation.Constructor.ContainingType);
                foreach (var typeArgument in operation.Constructor.TypeArguments)
                {
                    AddCannotBeStaticType(typeArgument);
                }
            }

            public void AnalyzeArrayCreation(OperationAnalysisContext context)
            {
                var operation = (IArrayCreationOperation)context.Operation;
                AddCannotBeStaticType(operation.Type);
            }

            public void AnalyzeInvocation(OperationAnalysisContext context)
            {
                var operation = (IInvocationOperation)context.Operation;
                foreach (var typeArgument in operation.TargetMethod.TypeArguments)
                {
                    AddCannotBeStaticType(typeArgument);
                }
            }

            public void AnalyzeCompilationEnd(CompilationAnalysisContext context)
            {
                foreach (var @class in _potentialClasses)
                {
                    if (_cannotBeStaticClasses.Contains(@class))
                        continue;

                    context.ReportDiagnostic(s_rule, @class);
                }
            }

            private void AddCannotBeStaticType(ITypeSymbol typeSymbol)
            {
                lock (_cannotBeStaticClasses)
                {
                    _cannotBeStaticClasses.Add(typeSymbol);
                    if (!typeSymbol.Equals(typeSymbol.OriginalDefinition))
                    {
                        AddCannotBeStaticType(typeSymbol.OriginalDefinition);
                    }

                    if (typeSymbol is IArrayTypeSymbol arrayTypeSymbol)
                    {
                        AddCannotBeStaticType(arrayTypeSymbol.ElementType);
                    }

                    if (typeSymbol is INamedTypeSymbol namedTypeSymbol)
                    {
                        foreach (var typeArgument in namedTypeSymbol.TypeArguments)
                        {
                            AddCannotBeStaticType(typeArgument);
                        }
                    }
                }
            }
        }
    }
}

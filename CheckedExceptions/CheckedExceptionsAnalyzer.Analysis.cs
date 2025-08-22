using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Sundstrom.CheckedExceptions;

partial class CheckedExceptionsAnalyzer
{
    private static void AnalyzeExceptionsInTryBlock(SyntaxNodeAnalysisContext context, TryStatementSyntax tryStatement, CatchClauseSyntax generalCatchClause, ThrowStatementSyntax throwStatement, AnalyzerSettings settings)
    {
        var semanticModel = context.SemanticModel;

        // Collect exceptions that can be thrown in the try block
        var thrownExceptions = CollectUnhandledExceptions(context, tryStatement.Block, settings);

        // Collect exception types handled by preceding catch clauses
        var handledExceptions = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var catchClause in tryStatement.Catches)
        {
            if (catchClause == generalCatchClause)
                break; // Stop at the general catch clause

            if (catchClause.Declaration is not null)
            {
                var catchType = semanticModel.GetTypeInfo(catchClause.Declaration.Type).Type as INamedTypeSymbol;
                if (catchType is not null)
                {
                    handledExceptions.Add(catchType);
                }
            }
            else
            {
                // General catch clause before our general catch; handles all exceptions
                handledExceptions = null;
                break;
            }
        }

        if (handledExceptions is null)
        {
            // All exceptions are handled by a previous general catch
            return;
        }

        // For each thrown exception, check if it is handled
        foreach (var exceptionType in thrownExceptions.Distinct(SymbolEqualityComparer.Default).OfType<INamedTypeSymbol>())
        {
            var exceptionName = exceptionType.ToDisplayString();

            if (FilterIgnored(settings, exceptionName))
            {
                // Completely ignore this exception
                continue;
            }
            else if (settings.InformationalExceptions.TryGetValue(exceptionName, out var mode))
            {
                if (ShouldIgnore(throwStatement, mode))
                {
                    // Report as THROW002 (Info level)
                    var diagnostic = Diagnostic.Create(RuleIgnoredException, GetSignificantLocation(throwStatement), exceptionType.Name);
                    context.ReportDiagnostic(diagnostic);
                    continue;
                }
            }

            // ① handled by any typed catch BEFORE the general catch?
            bool isHandledByPreceding = handledExceptions.Any(h =>
                SymbolEqualityComparer.Default.Equals(exceptionType, h) ||
                exceptionType.InheritsFrom(h));

            // ② handled by any INNER try/catch surrounding the rethrow (between throw; and the outer catch)?
            bool isHandledByEnclosingInnerTry =
                IsRethrowHandledByEnclosingTry(semanticModel, throwStatement, generalCatchClause, exceptionType);

            bool isDeclared = IsExceptionDeclaredInMember(context, tryStatement, exceptionType);

            if (!isHandledByPreceding && !isHandledByEnclosingInnerTry && !isDeclared)
            {
                var properties = ImmutableDictionary.Create<string, string?>()
                    .Add("ExceptionType", exceptionType.Name);

                var diagnostic = Diagnostic.Create(
                    RuleUnhandledException,
                    GetSignificantLocation(throwStatement),
                    properties,
                    exceptionType.Name);

                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private static bool IsRethrowHandledByEnclosingTry(
    SemanticModel semanticModel,
    ThrowStatementSyntax throwStatement,
    CatchClauseSyntax outerCatch,
    INamedTypeSymbol exceptionType)
    {
        for (SyntaxNode? n = throwStatement.Parent; n is not null && n != outerCatch; n = n.Parent)
        {
            if (n is TryStatementSyntax t)
            {
                foreach (var c in t.Catches)
                {
                    // general catch => handles all
                    if (c.Declaration is null)
                        return true;

                    var caught = semanticModel.GetTypeInfo(c.Declaration.Type).Type as INamedTypeSymbol;
                    if (caught is null) continue;

                    if (SymbolEqualityComparer.Default.Equals(exceptionType, caught) ||
                        exceptionType.InheritsFrom(caught))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static HashSet<INamedTypeSymbol> CollectUnhandledExceptions(SyntaxNodeAnalysisContext context, BlockSyntax block, AnalyzerSettings settings)
    {
        var unhandledExceptions = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var statement in block.Statements)
        {
            if (statement is TryStatementSyntax tryStatement)
            {
                // Recursively collect exceptions from the inner try block
                var innerUnhandledExceptions = CollectUnhandledExceptions(context, tryStatement.Block, settings);

                // Remove exceptions that are caught by the inner catch clauses
                var caughtExceptions = GetCaughtExceptions(tryStatement.Catches, context.SemanticModel);
                innerUnhandledExceptions.RemoveWhere(exceptionType =>
                    IsExceptionCaught(exceptionType, caughtExceptions));

                // Add any exceptions that are not handled in the inner try block
                unhandledExceptions.UnionWith(innerUnhandledExceptions);
            }
            else
            {
                // Collect exceptions thrown in this statement
                var statementExceptions = CollectExceptionsFromStatement(statement, context.Compilation, context.SemanticModel, settings);

                // Add them to the unhandled exceptions
                unhandledExceptions.UnionWith(statementExceptions);
            }
        }

        return unhandledExceptions;
    }

    private static HashSet<INamedTypeSymbol> CollectExceptionsFromStatement(StatementSyntax statement, Compilation compilation, SemanticModel semanticModel, AnalyzerSettings settings)
    {
        var exceptions = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        // Collect exception from throw statement
        if (statement is ThrowStatementSyntax throwStatement)
        {
            if (throwStatement.Expression is not null)
            {
                var exceptionType = semanticModel.GetTypeInfo(throwStatement.Expression).Type as INamedTypeSymbol;
                if (exceptionType is not null)
                {
                    if (ShouldIncludeException(exceptionType, throwStatement, settings))
                    {
                        exceptions.Add(exceptionType);
                    }
                }
            }
        }

        CollectExceptionsFromExpression(statement, compilation, semanticModel, settings, exceptions);

        return exceptions;
    }

    private static HashSet<INamedTypeSymbol> CollectExceptionsFromExpression(SyntaxNode expression, Compilation compilation, SemanticModel semanticModel, AnalyzerSettings settings)
    {
        HashSet<INamedTypeSymbol> exceptions = [];
        CollectExceptionsFromExpression(expression, compilation, semanticModel, settings, exceptions);
        return exceptions;
    }

    private static void CollectExceptionsFromExpression(SyntaxNode expression, Compilation compilation, SemanticModel semanticModel, AnalyzerSettings settings, HashSet<INamedTypeSymbol> exceptions)
    {
        // Collect exceptions from throw expressions
        var throwExpressions = expression.DescendantNodesAndSelf().OfType<ThrowExpressionSyntax>();
        foreach (var throwExpression in throwExpressions)
        {
            if (throwExpression.Expression is not null)
            {
                var exceptionType = semanticModel.GetTypeInfo(throwExpression.Expression).Type as INamedTypeSymbol;
                if (exceptionType is not null)
                {
                    if (ShouldIncludeException(exceptionType, throwExpression, settings))
                    {
                        exceptions.Add(exceptionType);
                    }
                }
            }
        }

        // Collect exceptions from method calls and other expressions
        var invocationExpressions = expression.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>();
        foreach (var invocation in invocationExpressions)
        {
            // Handle Lambda

            var methodSymbol = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
            if (methodSymbol is not null)
            {
                // Handle delegate invokes by getting the target method symbol
                if (methodSymbol.MethodKind == MethodKind.DelegateInvoke)
                {
                    var targetMethodSymbol = GetTargetMethodSymbol(semanticModel, invocation) ?? methodSymbol;

                    var exceptionTypes = GetExceptionTypes(targetMethodSymbol);

                    foreach (var exceptionType in exceptionTypes)
                    {
                        if (ShouldIncludeException(exceptionType, invocation, settings))
                        {
                            exceptions.Add(exceptionType);
                        }
                    }
                }
                else
                {
                    var exceptionTypes = GetExceptionTypes(methodSymbol);

                    if (settings.IsXmlInteropEnabled)
                    {
                        // Get exceptions from XML documentation
                        var xmlExceptionTypes = GetExceptionTypesFromDocumentationCommentXml(semanticModel.Compilation, methodSymbol);

                        xmlExceptionTypes = ProcessNullable(compilation, semanticModel, invocation, methodSymbol, xmlExceptionTypes);

                        if (xmlExceptionTypes.Any())
                        {
                            exceptionTypes.AddRange(xmlExceptionTypes.Select(x => x.ExceptionType));
                        }
                    }

                    foreach (var exceptionType in exceptionTypes)
                    {
                        if (ShouldIncludeException(exceptionType, invocation, settings))
                        {
                            exceptions.Add(exceptionType);
                        }
                    }
                }
            }
        }

        var objectCreations = expression.DescendantNodesAndSelf().OfType<BaseObjectCreationExpressionSyntax>();
        foreach (var objectCreation in objectCreations)
        {
            var methodSymbol = semanticModel.GetSymbolInfo(objectCreation).Symbol as IMethodSymbol;
            if (methodSymbol is not null)
            {
                var exceptionTypes = GetExceptionTypes(methodSymbol);

                if (settings.IsXmlInteropEnabled)
                {
                    // Get exceptions from XML documentation
                    var xmlExceptionTypes = GetExceptionTypesFromDocumentationCommentXml(semanticModel.Compilation, methodSymbol);

                    xmlExceptionTypes = ProcessNullable(compilation, semanticModel, objectCreation, methodSymbol, xmlExceptionTypes);

                    if (xmlExceptionTypes.Any())
                    {
                        exceptionTypes.AddRange(xmlExceptionTypes.Select(x => x.ExceptionType));
                    }
                }

                foreach (var exceptionType in exceptionTypes)
                {
                    if (ShouldIncludeException(exceptionType, objectCreation, settings))
                    {
                        exceptions.Add(exceptionType);
                    }
                }
            }
        }

        // Collect from MemberAccess and Identifier
        var memberAccessExpressions = expression.DescendantNodesAndSelf().OfType<MemberAccessExpressionSyntax>();
        foreach (var memberAccess in memberAccessExpressions)
        {
            var propertySymbol = semanticModel.GetSymbolInfo(memberAccess).Symbol as IPropertySymbol;
            if (propertySymbol is not null)
            {
                HashSet<INamedTypeSymbol> exceptionTypes = GetPropertyExceptionTypes(compilation, semanticModel, memberAccess, propertySymbol, settings);

                foreach (var exceptionType in exceptionTypes)
                {
                    if (ShouldIncludeException(exceptionType, memberAccess, settings))
                    {
                        exceptions.Add(exceptionType);
                    }
                }
            }
        }

        var elementAccessExpressions = expression.DescendantNodesAndSelf().OfType<ElementAccessExpressionSyntax>();
        foreach (var elementAccess in elementAccessExpressions)
        {
            var propertySymbol = semanticModel.GetSymbolInfo(elementAccess).Symbol as IPropertySymbol;
            if (propertySymbol is not null)
            {
                HashSet<INamedTypeSymbol> exceptionTypes = GetPropertyExceptionTypes(compilation, semanticModel, elementAccess, propertySymbol, settings);

                foreach (var exceptionType in exceptionTypes)
                {
                    if (ShouldIncludeException(exceptionType, elementAccess, settings))
                    {
                        exceptions.Add(exceptionType);
                    }
                }
            }
        }

        var identifierExpressions = expression.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>();
        foreach (var identifier in identifierExpressions)
        {
            var propertySymbol = semanticModel.GetSymbolInfo(identifier).Symbol as IPropertySymbol;
            if (propertySymbol is not null)
            {
                HashSet<INamedTypeSymbol> exceptionTypes = GetPropertyExceptionTypes(compilation, semanticModel, identifier, propertySymbol, settings);

                foreach (var exceptionType in exceptionTypes)
                {
                    if (exceptionType is not null)
                    {
                        if (ShouldIncludeException(exceptionType, identifier, settings))
                        {
                            exceptions.Add(exceptionType);
                        }
                    }
                }
            }
        }

        var castExpressions = expression.DescendantNodesAndSelf().OfType<CastExpressionSyntax>();
        foreach (var castExpression in castExpressions)
        {
            var sourceType = semanticModel.GetTypeInfo(castExpression.Expression).Type;
            var targetType = semanticModel.GetTypeInfo(castExpression.Type).Type;

            if (sourceType is null || targetType is null)
                return;

            INamedTypeSymbol? invalidCastException = CheckCastExpression(compilation, semanticModel, castExpression, targetType);

            if (invalidCastException is not null)
            {
                if (ShouldIncludeException(invalidCastException, castExpression, settings))
                {
                    exceptions.Add(invalidCastException);
                }
            }
        }
    }

    private static HashSet<INamedTypeSymbol> GetCaughtExceptions(SyntaxList<CatchClauseSyntax> catchClauses, SemanticModel semanticModel)
    {
        var caughtExceptions = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var catchClause in catchClauses)
        {
            if (catchClause.Declaration is not null)
            {
                INamedTypeSymbol? catchType = GetCaughtException(catchClause, semanticModel);
                if (catchType is not null)
                {
                    caughtExceptions.Add(catchType);
                }
            }
            else
            {
                // General catch clause catches all exceptions
                caughtExceptions = null;
                break;
            }
        }

        return caughtExceptions;
    }

    private static INamedTypeSymbol? GetCaughtException(CatchClauseSyntax catchClause, SemanticModel semanticModel)
    {
        if (catchClause.Declaration?.Type is null)
            return null;

        return semanticModel.GetTypeInfo(catchClause.Declaration.Type).Type as INamedTypeSymbol;
    }

    private static bool IsExceptionCaught(INamedTypeSymbol exceptionType, HashSet<INamedTypeSymbol>? caughtExceptions)
    {
        if (caughtExceptions is null)
        {
            // general catch (catch without type) => swallows all
            return true;
        }

        return caughtExceptions.Any(catchType =>
            exceptionType.Equals(catchType, SymbolEqualityComparer.Default) ||
            exceptionType.InheritsFrom(catchType));
    }

    private static bool IsExceptionCaught(INamedTypeSymbol exceptionType, INamedTypeSymbol? catchType)
    {
        if (catchType is null)
        {
            // null means general catch
            return true;
        }

        return exceptionType.Equals(catchType, SymbolEqualityComparer.Default) ||
               exceptionType.InheritsFrom(catchType);
    }

    /// <summary>
    /// Determines if a node is within a catch block.
    /// </summary>
    private static bool IsWithinCatchBlock(SyntaxNode node, out CatchClauseSyntax catchClause)
    {
        catchClause = node.Ancestors().OfType<CatchClauseSyntax>().FirstOrDefault();
        return catchClause is not null;
    }

    /// <summary>
    /// Determines if a catch clause handles the specified exception type.
    /// </summary>
    private static bool CatchClauseHandlesException(CatchClauseSyntax catchClause, SemanticModel semanticModel, INamedTypeSymbol exceptionType)
    {
        if (catchClause.Declaration is null)
            return true; // Catch-all handles all exceptions

        var catchType = semanticModel.GetTypeInfo(catchClause.Declaration.Type).Type as INamedTypeSymbol;
        if (catchType is null)
            return false;

        // Check if the exceptionType matches or inherits from the catchType
        return exceptionType.Equals(catchType, SymbolEqualityComparer.Default) ||
               exceptionType.InheritsFrom(catchType);
    }

    /// <summary>
    /// Determines if an exception is handled by any enclosing try-catch blocks.
    /// </summary>
    private static bool IsExceptionHandledByEnclosingTryCatch(SyntaxNode node, INamedTypeSymbol exceptionType, SemanticModel semanticModel)
    {
        SyntaxNode? prevNode = null;

        var current = node.Parent;
        while (current is not null)
        {
            // Stop here since the throwing node is within a lambda or a local function
            // and the boundary has been reached.
            if (current is AnonymousFunctionExpressionSyntax
                or LocalFunctionStatementSyntax)
            {
                return false;
            }

            if (current is TryStatementSyntax tryStatement)
            {
                // Prevents analysis within the first try-catch,
                // when coming from either a catch clause or a finally clause. 

                // Skip if the node is within a catch or finally block of the current try statement
                bool isInCatchOrFinally = tryStatement.Catches.Any(c => c.Contains(node)) ||
                                          (tryStatement.Finally is not null && tryStatement.Finally.Contains(node));


                if (!isInCatchOrFinally)
                {
                    foreach (var catchClause in tryStatement.Catches)
                    {
                        if (CatchClauseHandlesException(catchClause, semanticModel, exceptionType))
                        {
                            return true;
                        }
                    }
                }
            }

            prevNode = current;
            current = current.Parent;
        }

        return false; // Exception is not handled by any enclosing try-catch
    }

    private static void AnalyzeFunctionAttributes(SyntaxNode node, IEnumerable<AttributeSyntax> attributes, SemanticModel semanticModel, SyntaxNodeAnalysisContext context)
    {
        var settings = GetAnalyzerSettings(context.Options);

        var throwsAttributes = attributes
            .Where(attr => IsThrowsAttribute(attr, semanticModel))
            .ToList();

        CheckXmlDocsForUndeclaredExceptions_Method(throwsAttributes, context);

        if (settings.IsControlFlowAnalysisEnabled)
        {
            AnalyzeControlFlow(throwsAttributes, context);
        }

        if (throwsAttributes.Count is 0)
            return;

        CheckForGeneralExceptionThrows(context, throwsAttributes);

        if (throwsAttributes.Any())
        {
            CheckForDuplicateThrowsDeclarations(throwsAttributes, context);
            CheckForRedundantThrowsHandledByDeclaredSuperClass(throwsAttributes, context);
        }
    }

    private static void AnalyzeLinqOperation(SyntaxNodeAnalysisContext context, IMethodSymbol methodSymbol, HashSet<INamedTypeSymbol> exceptionTypes, InvocationExpressionSyntax invocation)
    {
        if (IsLinqExtension(methodSymbol))
        {
            var name = methodSymbol.Name;

            if (name == "Cast")
            {
                // Remove InvalidOperationException from XML — it’s not actually thrown here
                var invalidCastExc = context.Compilation.GetTypeByMetadataName("System.InvalidCastException");
                if (invalidCastExc is not null)
                    exceptionTypes.RemoveWhere(e => SymbolEqualityComparer.Default.Equals(e, invalidCastExc));
            }
        }

        var settings = GetAnalyzerSettings(context.Options);

        CollectLinqExceptions(invocation, exceptionTypes, context.Compilation, context.SemanticModel, settings, context.CancellationToken);
    }
}
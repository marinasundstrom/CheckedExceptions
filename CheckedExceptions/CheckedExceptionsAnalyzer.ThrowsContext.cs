using System;
using System.Threading;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Sundstrom.CheckedExceptions;

partial class CheckedExceptionsAnalyzer
{
    private readonly struct ThrowsContext
    {
        public SemanticModel SemanticModel { get; }
        public AnalyzerOptions Options { get; }
        public Action<Diagnostic> ReportDiagnostic { get; }
        public CancellationToken CancellationToken { get; }

        public ThrowsContext(
            SemanticModel semanticModel,
            AnalyzerOptions options,
            Action<Diagnostic> reportDiagnostic,
            CancellationToken cancellationToken)
        {
            SemanticModel = semanticModel;
            Options = options;
            ReportDiagnostic = reportDiagnostic;
            CancellationToken = cancellationToken;
        }
    }
}
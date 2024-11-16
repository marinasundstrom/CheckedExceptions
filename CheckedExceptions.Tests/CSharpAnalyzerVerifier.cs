using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

using static Microsoft.CodeAnalysis.Testing.ReferenceAssemblies;

namespace CheckedExceptions.Test;

public static class CSharpAnalyzerVerifier<TAnalyzer, TVerifier>
     where TAnalyzer : DiagnosticAnalyzer, new()
     where TVerifier : IVerifier, new()
{
    public static DiagnosticResult Diagnostic(string diagnosticId)
        => AnalyzerVerifier<TAnalyzer, AnalyzerTest, TVerifier>.Diagnostic(diagnosticId);

    public static Task VerifyAnalyzerAsync(string source, params DiagnosticResult[] expected)
    {
        var test = new AnalyzerTest
        {
            TestCode = source,
        };

        var allDiagnostics = CheckedExceptionsAnalyzer.AllDiagnosticsIds;

        test.DisabledDiagnostics.AddRange(allDiagnostics.Except(expected.Select(x => x.Id)));

        test.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(ThrowsAttribute).Assembly.Location));
        test.TestState.ReferenceAssemblies = Net.Net90;

        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }

    private class AnalyzerTest : CSharpAnalyzerTest<TAnalyzer, TVerifier>
    {

    }
}
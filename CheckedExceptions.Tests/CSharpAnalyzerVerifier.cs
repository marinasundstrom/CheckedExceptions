using System.Diagnostics.CodeAnalysis;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

using static Microsoft.CodeAnalysis.Testing.ReferenceAssemblies;

namespace Sundstrom.CheckedExceptions.Tests;

public static class CSharpAnalyzerVerifier<TAnalyzer, TVerifier>
     where TAnalyzer : DiagnosticAnalyzer, new()
     where TVerifier : IVerifier, new()
{
    public static DiagnosticResult Diagnostic(string diagnosticId)
        => AnalyzerVerifier<TAnalyzer, AnalyzerTest, TVerifier>.Diagnostic(diagnosticId);

    public static DiagnosticResult IsThrown(string exceptionType)
        => AnalyzerVerifier<TAnalyzer, AnalyzerTest, TVerifier>.Diagnostic("THROW001")
        .WithArguments(exceptionType, THROW001Verbs.Is);

    public static DiagnosticResult MightBeThrown(string exceptionType)
        => AnalyzerVerifier<TAnalyzer, AnalyzerTest, TVerifier>.Diagnostic("THROW001")
        .WithArguments(exceptionType, THROW001Verbs.MightBe);


    public static DiagnosticResult Informational(string exceptionType)
        => AnalyzerVerifier<TAnalyzer, AnalyzerTest, TVerifier>.Diagnostic("THROW002")
        .WithArguments(exceptionType);

    public static async Task VerifyAnalyzerAsync([StringSyntax("c#-test")] string source, params DiagnosticResult[] expected)
    {
        await VerifyAnalyzerAsync(source, (test) =>
        {
            if (expected.Any())
            {
                var allDiagnostics = CheckedExceptionsAnalyzer.AllDiagnosticsIds;

                test.DisabledDiagnostics.AddRange(allDiagnostics.Except(expected.Select(x => x.Id)));
            }

            test.ExpectedDiagnostics.AddRange(expected);
        });
    }

    public static async Task VerifyAnalyzerAsync2([StringSyntax("c#-test")] string source, params DiagnosticResult[] expected)
    {
        await VerifyAnalyzerAsync(source, (test) =>
        {
            if (expected.Any())
            {
                var allDiagnostics = CheckedExceptionsAnalyzer.AllDiagnosticsIds;

                test.DisabledDiagnostics.AddRange(allDiagnostics.Except(expected.Select(x => x.Id)));
            }

            test.ExpectedDiagnostics.AddRange(expected);

            test.TestState.AdditionalFiles.Add(("CheckedExceptions.settings.json",
            """"
            {
                "ignoredExceptions": [
                    "System.NotImplementedException"
                ],
                "informationalExceptions": [
                    "System.IO.IOException",
                    "System.TimeoutException"
                ]
            }
            """"));
        });
    }

    public static Task VerifyAnalyzerAsync([StringSyntax("c#-test")] string source, Action<AnalyzerTest>? setup = null)
    {
        var test = new AnalyzerTest
        {
            TestCode = source
        };

        test.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(ThrowsAttribute).Assembly.Location));
        test.TestState.ReferenceAssemblies = Net.Net90;

        setup?.Invoke(test);

        return test.RunAsync();
    }

    public class AnalyzerTest : CSharpAnalyzerTest<TAnalyzer, TVerifier>
    {

    }
}
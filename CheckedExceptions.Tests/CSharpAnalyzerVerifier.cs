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

    public static DiagnosticResult UnhandledException(string exceptionType)
        => AnalyzerVerifier<TAnalyzer, AnalyzerTest, TVerifier>.Diagnostic("THROW001")
        .WithArguments(exceptionType);

    public static DiagnosticResult Informational(string exceptionType)
        => AnalyzerVerifier<TAnalyzer, AnalyzerTest, TVerifier>.Diagnostic("THROW002")
        .WithArguments(exceptionType);

    public static DiagnosticResult AvoidDeclaringTypeException()
        => AnalyzerVerifier<TAnalyzer, AnalyzerTest, TVerifier>.Diagnostic("THROW003");

    public static DiagnosticResult AvoidThrowingTypeException()
        => AnalyzerVerifier<TAnalyzer, AnalyzerTest, TVerifier>.Diagnostic("THROW004");

    public static DiagnosticResult DuplicateExceptionDeclared(string exceptionType)
        => AnalyzerVerifier<TAnalyzer, AnalyzerTest, TVerifier>.Diagnostic("THROW005")
        .WithArguments(exceptionType);

    public static DiagnosticResult MissingThrowsOnBaseMember(string exceptionType, string memberName)
        => AnalyzerVerifier<TAnalyzer, AnalyzerTest, TVerifier>.Diagnostic("THROW006")
        .WithArguments(memberName, exceptionType);


    public static DiagnosticResult MissingThrowsFromBaseMember(string exceptionType, string memberName)
        => AnalyzerVerifier<TAnalyzer, AnalyzerTest, TVerifier>.Diagnostic("THROW007")
        .WithArguments(memberName, exceptionType);


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
                "informationalExceptions": {
                    "System.IO.IOException": "Always",
                    "System.TimeoutException": "Always"
                }
            }
            """"));
        });
    }

    public static Task VerifyAnalyzerAsync([StringSyntax("c#-test")] string source, Action<AnalyzerTest>? setup = null, bool executable = false)
    {
        var test = new AnalyzerTest
        {
            TestCode = source
        };

        test.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(ThrowsAttribute).Assembly.Location));
        test.TestState.ReferenceAssemblies = Net.Net90;

        if (executable)
        {
            test.TestState.OutputKind = OutputKind.ConsoleApplication;
        }

        setup?.Invoke(test);

        return test.RunAsync();
    }

    public class AnalyzerTest : CSharpAnalyzerTest<TAnalyzer, TVerifier>
    {

    }
}
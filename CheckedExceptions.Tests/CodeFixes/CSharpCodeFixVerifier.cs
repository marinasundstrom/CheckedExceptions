namespace Sundstrom.CheckedExceptions.Tests.CodeFixes;

using System.Diagnostics.CodeAnalysis;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;

using static Microsoft.CodeAnalysis.Testing.ReferenceAssemblies;

public static class CSharpCodeFixVerifier<TAnalyzer, TCodeFix, TVerifier>
        where TAnalyzer : DiagnosticAnalyzer, new()
        where TCodeFix : CodeFixProvider, new()
        where TVerifier : IVerifier, new()
{
    public static DiagnosticResult Diagnostic(string diagnosticId)
        => CodeFixVerifier<TAnalyzer, TCodeFix, CodeFixTest, TVerifier>.Diagnostic(diagnosticId);

    public static DiagnosticResult IsThrown(string exceptionType)
        => CodeFixVerifier<TAnalyzer, TCodeFix, CodeFixTest, TVerifier>.Diagnostic("THROW001")
        .WithArguments(exceptionType, THROW001Verbs.Is);

    public static DiagnosticResult MightBeThrown(string exceptionType)
        => CodeFixVerifier<TAnalyzer, TCodeFix, CodeFixTest, TVerifier>.Diagnostic("THROW001")
        .WithArguments(exceptionType, THROW001Verbs.MightBe);


    public static Task VerifyCodeFixAsync([StringSyntax("c#-test")] string source, DiagnosticResult expected, [StringSyntax("c#-test")] string? fixedSource = null, int? expectedIncrementalIterations = 1)
    {
        return VerifyCodeFixAsync(source, new[] { expected }, fixedSource, expectedIncrementalIterations);
    }

    public static Task VerifyCodeFixAsync([StringSyntax("c#-test")] string source, IEnumerable<DiagnosticResult> expected, [StringSyntax("c#-test")] string? fixedSource = null, int? expectedIncrementalIterations = 1)
    {
        var test = new CodeFixTest
        {
            TestCode = source,
            CodeFixTestBehaviors = CodeFixTestBehaviors.SkipFixAllCheck,
            NumberOfIncrementalIterations = expectedIncrementalIterations
        };

        if (fixedSource is not null)
        {
            test.FixedCode = fixedSource;
        }

        var allDiagnostics = CheckedExceptionsAnalyzer.AllDiagnosticsIds;

        if (expected is not null)
        {
            test.DisabledDiagnostics.AddRange(allDiagnostics.Except(expected.Select(x => x.Id)));
        }

        test.TestState.AdditionalReferences.Add(MetadataReference.CreateFromFile(typeof(ThrowsAttribute).Assembly.Location));
        test.TestState.ReferenceAssemblies = Net.Net90;

        if (expected is not null)
        {
            test.ExpectedDiagnostics.AddRange(expected);
        }

        return test.RunAsync();
    }

    private class CodeFixTest : CSharpCodeFixTest<TAnalyzer, TCodeFix, TVerifier>
    {

    }
}
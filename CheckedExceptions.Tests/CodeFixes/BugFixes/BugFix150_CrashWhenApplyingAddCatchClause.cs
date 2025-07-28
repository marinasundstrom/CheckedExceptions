namespace Sundstrom.CheckedExceptions.Tests.CodeFixes.BugFixes;

using System.Threading.Tasks;

using Microsoft.CodeAnalysis.Testing;

using Xunit;

using Verifier = CSharpCodeFixVerifier<CheckedExceptionsAnalyzer, AddCatchClauseToTryCodeFixProvider, Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

public class BugFix150_CrashWhenApplyingAddCatchClause
{
    [Fact]
    public async Task AddCatchClausesToTryWithoutCatchClauses()
    {
        var testCode = /*lang=c#-test*/ """
#nullable enable
using System;

try
{
    int.Parse("");
}
""";

        var expected = /*lang=c#-test*/ """
#nullable enable
using System;

try
{
    int.Parse("");
}
catch (FormatException formatException)
{
}
catch (OverflowException overflowException)
{
}
""";

        var expectedDiagnostic = Verifier.UnhandledException("FormatException")
             .WithSpan(6, 9, 6, 18);

        var expectedDiagnostic2 = Verifier.UnhandledException("OverflowException")
             .WithSpan(6, 9, 6, 18);

        // Expect fix to be registered and applied
        await Verifier.VerifyCodeFixAsync(testCode, [expectedDiagnostic, expectedDiagnostic2], expected, executable: true, expectedIncrementalIterations: 2, setup: e => e.CompilerDiagnostics = CompilerDiagnostics.None);
    }

    [Fact]
    public async Task AddCatchClausesToTryWithCatchClauses()
    {
        var testCode = /*lang=c#-test*/ """
#nullable enable
using System;

try
{
    int.Parse("");
}
catch (FormatException formatException)
{
}
""";

        var expected = /*lang=c#-test*/ """
#nullable enable
using System;

try
{
    int.Parse("");
}
catch (FormatException formatException)
{
}
catch (OverflowException overflowException)
{
}
""";

        var expectedDiagnostic2 = Verifier.UnhandledException("OverflowException")
             .WithSpan(6, 9, 6, 18);

        // Expect fix to be registered and applied
        await Verifier.VerifyCodeFixAsync(testCode, [expectedDiagnostic2], expected, executable: true, setup: e => e.CompilerDiagnostics = CompilerDiagnostics.None);
    }
}
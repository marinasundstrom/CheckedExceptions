namespace Sundstrom.CheckedExceptions.Tests.CodeFixes.BugFixes;

using System.Threading.Tasks;

using Microsoft.CodeAnalysis.Testing;

using Xunit;

using Verifier = CSharpCodeFixVerifier<CheckedExceptionsAnalyzer, RemoveRedundantCatchClauseCodeFixProvider, Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

public class BugFix275
{
    [Fact]
    public async Task Test()
    {
        var testCode = /* lang=c#-test */  """
            #nullable enable
            using System;

            try 
            {
                int.Parse("");
            }
            catch (Exception exc) 
            {
            }
            catch (FormatException exc) 
            {
            }
            catch (OverflowException exc) 
            {
            }
            """;

        var fixedCode = /* lang=c#-test */  """
            #nullable enable
            using System;

            try 
            {
                int.Parse("");
            }
            catch (Exception exc) 
            {
            }
            """;

        var expected = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdRedundantCatchClause)
            .WithArguments("FormatException")
            .WithSpan(11, 1, 11, 6);

        var expected2 = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdRedundantCatchClause)
            .WithArguments("OverflowException")
            .WithSpan(14, 1, 14, 6);

        await Verifier.VerifyCodeFixAsync(testCode, [expected, expected2], fixedCode, setup: opt =>
        {
            opt.DisabledDiagnostics.Remove(CheckedExceptionsAnalyzer.DiagnosticIdRedundantCatchClause);

            opt.CompilerDiagnostics = CompilerDiagnostics.None;
        }, expectedIncrementalIterations: 2);

    }
}

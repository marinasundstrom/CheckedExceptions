namespace Sundstrom.CheckedExceptions.Tests.CodeFixes.BugFixes;

using System.Threading.Tasks;

using Microsoft.CodeAnalysis.Testing;

using Xunit;

using Verifier = CSharpCodeFixVerifier<CheckedExceptionsAnalyzer, AddThrowsDeclarationFromXmlDocCodeFixProvider, Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

public class BugFix234
{
    [Fact]
    public async Task Should_AddDeclarationsFromXmlDoc_ToLocalFunction()
    {
        var testCode = /* lang=c#-test */  """
            #nullable enable
            using System;

            class Program
            {
                public void WithLocalFunction()
                {
                    /// <exception cref="System.InvalidCastException" />
                    void Test()
                    {

                    }
                }
            }
            """;

        var fixedCode = /* lang=c#-test */  """
            #nullable enable
            using System;
            
            class Program
            {
                public void WithLocalFunction()
                {
                    /// <exception cref="System.InvalidCastException" />
                    [Throws(typeof(InvalidCastException))]
                    void Test()
                    {

                    }
                }
            }
            """;

        var expectedDiagnostic = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdXmlDocButNoThrows)
            .WithArguments("InvalidCastException")
            .WithSpan(9, 14, 9, 18);

        await Verifier.VerifyCodeFixAsync(testCode, [expectedDiagnostic], fixedCode, setup: opt =>
        {
            opt.CompilerDiagnostics = CompilerDiagnostics.None;
        });

    }
}
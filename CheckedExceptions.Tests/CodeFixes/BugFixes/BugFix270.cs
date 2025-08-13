namespace Sundstrom.CheckedExceptions.Tests.CodeFixes.BugFixes;

using System.Threading.Tasks;

using Microsoft.CodeAnalysis.Testing;

using Xunit;

using Verifier = CSharpCodeFixVerifier<CheckedExceptionsAnalyzer, AddThrowsDeclarationFromXmlDocCodeFixProvider, Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

public class BugFix270
{
    [Fact]
    public async Task Test()
    {
        var testCode = /* lang=c#-test */  """
            #nullable enable
            using System;

            class Program
            {
                /// <exception cref="System.InvalidOperationException" />
                public static int Foo2
                {
                    get
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
                /// <exception cref="System.InvalidOperationException" />
                public static int Foo2
                {
                    [Throws(typeof(InvalidOperationException))]
                    get
                    {

                    }
                }
            }
            """;

        var expectedDiagnostic = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdXmlDocButNoThrows)
            .WithArguments("InvalidOperationException")
            .WithSpan(9, 9, 9, 12);

        await Verifier.VerifyCodeFixAsync(testCode, [expectedDiagnostic], fixedCode, setup: opt =>
        {
            opt.CompilerDiagnostics = CompilerDiagnostics.None;
        });

    }
}

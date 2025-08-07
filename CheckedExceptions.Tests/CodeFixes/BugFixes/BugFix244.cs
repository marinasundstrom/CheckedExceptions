namespace Sundstrom.CheckedExceptions.Tests.CodeFixes.BugFixes;

using System.Threading.Tasks;

using Microsoft.CodeAnalysis.Testing;

using Xunit;

using Verifier = CSharpCodeFixVerifier<CheckedExceptionsAnalyzer, AddCatchClauseToTryCodeFixProvider, Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

public class BugFix244
{
    [Fact]
    public async Task Test()
    {
        var testCode = /* lang=c#-test */  """
            #nullable enable
            using System;

            class Program
            {
                public void Foo32()
                {
                    try
                    {
                        if (true)
                            throw new InvalidCastException();
                    }
                }
            }
            """;

        var fixedCode = /* lang=c#-test */  """
            #nullable enable
            using System;

            class Program
            {
                public void Foo32()
                {
                    try
                    {
                        if (true)
                            throw new InvalidCastException();
                    }
                    catch (InvalidCastException invalidCastException)
                    {
                    }
                }
            }
            """;

        var expectedDiagnostic = Verifier.UnhandledException("InvalidCastException")
            .WithSpan(11, 17, 11, 50);

        await Verifier.VerifyCodeFixAsync(testCode, [expectedDiagnostic], fixedCode, setup: opt =>
        {
            opt.CompilerDiagnostics = CompilerDiagnostics.None;
        });

    }
}
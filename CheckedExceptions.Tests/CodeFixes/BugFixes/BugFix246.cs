namespace Sundstrom.CheckedExceptions.Tests.CodeFixes.BugFixes;

using System.Threading.Tasks;

using Microsoft.CodeAnalysis.Testing;

using Xunit;

using Verifier = CSharpCodeFixVerifier<CheckedExceptionsAnalyzer, SurroundWithTryCatchCodeFixProvider, Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

public class BugFix246
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
                    if (true)
                        throw new InvalidCastException();

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
                    if (true)
                    {
                        try
                        {
                            throw new InvalidCastException();
                        }
                        catch (InvalidCastException invalidCastException)
                        {
                        }
                    }
                }
            }
            """;

        var expectedDiagnostic = Verifier.UnhandledException("InvalidCastException")
            .WithSpan(9, 13, 9, 46);

        await Verifier.VerifyCodeFixAsync(testCode, [expectedDiagnostic], fixedCode, setup: opt =>
        {
            opt.CompilerDiagnostics = CompilerDiagnostics.None;
        });

    }
}

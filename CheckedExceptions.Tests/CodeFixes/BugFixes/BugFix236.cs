namespace Sundstrom.CheckedExceptions.Tests.CodeFixes.BugFixes;

using System.Threading.Tasks;

using Microsoft.CodeAnalysis.Testing;

using Xunit;

using Verifier = CSharpCodeFixVerifier<CheckedExceptionsAnalyzer, AddThrowsDeclarationCodeFixProvider, Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

public class BugFix236
{
    [Fact]
    public async Task Should_AddDeclarations_ForThrow()
    {
        var testCode = /* lang=c#-test */  """
            #nullable enable
            using System;

            class Program
            {
                public void Foo31()
                {
                    try
                    {
                        MethodThatThrows();
                    }
                    catch
                    {
                        throw;
                    }
                }

                [Throws(typeof(InvalidOperationException))]
                public void MethodThatThrows()
                {
                    throw new InvalidOperationException();
                }
            }
            """;

        var fixedCode = /* lang=c#-test */  """
            #nullable enable
            using System;

            class Program
            {
                [Throws(typeof(InvalidOperationException))]
                public void Foo31()
                {
                    try
                    {
                        MethodThatThrows();
                    }
                    catch
                    {
                        throw;
                    }
                }

                [Throws(typeof(InvalidOperationException))]
                public void MethodThatThrows()
                {
                    throw new InvalidOperationException();
                }
            }
            """;

        var expectedDiagnostic = Verifier.UnhandledException("InvalidOperationException")
            .WithSpan(14, 13, 14, 19);

        await Verifier.VerifyCodeFixAsync(testCode, [expectedDiagnostic], fixedCode, setup: opt =>
        {
            opt.CompilerDiagnostics = CompilerDiagnostics.None;
        });

    }
}
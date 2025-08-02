namespace Sundstrom.CheckedExceptions.Tests.CodeFixes.BugFixes;

using System.Threading.Tasks;

using Microsoft.CodeAnalysis.Testing;

using Xunit;

using Verifier = CSharpCodeFixVerifier<CheckedExceptionsAnalyzer, SurroundWithTryCatchCodeFixProvider, Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

public class BugFix197
{
    [Fact]
    public async Task Sample()
    {
        var testCode = /* lang=c#-test */  """
            #nullable enable
            using System;
            using System.Linq;

            record WeatherForecast(int Day);

            class Program
            {
                void Foo()
                {
                    var forecast = (new [] { 1, 2, 3 }).Select(index => 
                        new WeatherForecast(DateTime.Now.AddDays(index).Day));
                }
            }
            """;

        var fixedCode = /* lang=c#-test */  """
            #nullable enable
            using System;
            using System.Linq;

            record WeatherForecast(int Day);

            class Program
            {
                void Foo()
                {
                    var forecast = (new [] { 1, 2, 3 }).Select(index =>
                    {
                        try
                        {
                            return new WeatherForecast(DateTime.Now.AddDays(index).Day);
                        }
                        catch (ArgumentOutOfRangeException argumentOutOfRangeException)
                        {
                        }
                    });
                }
            }
            """;

        var expectedDiagnostic = Verifier.UnhandledException("ArgumentOutOfRangeException")
            .WithSpan(12, 46, 12, 60);

        await Verifier.VerifyCodeFixAsync(testCode, [expectedDiagnostic], fixedCode, setup: opt =>
        {
            opt.CompilerDiagnostics = CompilerDiagnostics.None;
        });

    }
}
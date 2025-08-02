namespace Sundstrom.CheckedExceptions.Tests.CodeFixes.BugFixes;

using System.Threading.Tasks;

using Microsoft.CodeAnalysis.Testing;

using Xunit;

using Verifier = CSharpCodeFixVerifier<CheckedExceptionsAnalyzer, SurroundWithTryCatchCodeFixProvider, Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

public class BugFix
{
    [Fact]
    public async Task Sample()
    {
        var testCode = /* lang=c#-test */  """
            #nullable enable
            using System;
            using System.Linq;

            record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
            {
                public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
            }

            class Program
            {
                void Foo()
                {
                    var forecast = (new[] { 0, 2, 3 }).Select(index =>
                        new WeatherForecast
                        (
                            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                            10, // Random.Shared.Next(-20, 55),
                            summaries[Random.Shared.Next(summaries.Length)]
                        ))
                        .ToArray();
                }
            }
            """;

        var fixedCode = /* lang=c#-test */  """
            #nullable enable
            using System;
            using System.Linq;

            record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
            {
                public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
            }

            class Program
            {
                void Foo()
                {
                    var forecast = (new[] { 0, 2, 3 }).Select(index =>
                    {
                        try
                        {
                            return new WeatherForecast
                                                                                   (
                                                                                       DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                                                                                       10, // Random.Shared.Next(-20, 55),
                                                                                       summaries[Random.Shared.Next(summaries.Length)]
                                                                                   );
                        }
                        catch (ArgumentOutOfRangeException argumentOutOfRangeException)
                        {
                        }
                    })
                        .ToArray();
                }
            }
            """;

        var expectedDiagnostic = Verifier.UnhandledException("ArgumentOutOfRangeException")
            .WithSpan(17, 52, 17, 66);

        await Verifier.VerifyCodeFixAsync(testCode, [expectedDiagnostic], fixedCode, setup: opt =>
        {
            opt.CompilerDiagnostics = CompilerDiagnostics.None;
        });

    }
}
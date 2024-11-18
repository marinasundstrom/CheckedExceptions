using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

namespace CheckedExceptions.Tests;

using Verifier = CSharpAnalyzerVerifier<CheckedExceptionsAnalyzer, DefaultVerifier>;

public partial class TryCatchTest
{
    [Fact]
    public async Task Should_NotReportDiagnosticForSingleExceptionInNestedTry()
    {
        var test = """
            using System;
            using System.IO;

            public class Test
            {
                public void Run()
                {
                    try
                    {
                        try
                        {
                            Console.WriteLine("Hello");
                        }
                        catch (ArgumentException)
                        {

                        }
                    }
                    catch (IOException)
                    {

                    }
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task Should_NotReportDiagnosticWhenMultipleExceptionsInNestedTry()
    {
        var test = """
            using System;
            using System.IO;
            using System.Net.Http;
            using System.Threading.Tasks;

            public class Test
            {
                public async Task Run()
                {
                    try
                    {
                        try
                        {
                            var httpClient = new HttpClient()
                            {
                                BaseAddress = new Uri("https://www.scrapethissite.com")
                            };
                            var str = await httpClient.GetStringAsync("/");

                            Console.WriteLine(str);
                        }
                        catch (ArgumentException)
                        {

                        }
                        catch (InvalidOperationException)
                        {

                        }
                        catch (HttpRequestException)
                        {

                        }
                        catch (TaskCanceledException)
                        {

                        }
                        catch (UriFormatException)
                        {

                        }
                    }
                    catch (IOException)
                    {

                    }
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task Should_HandleNestedTryCatchWithDifferentExceptions()
    {
        var test = """
        using System;
        using System.IO;

        public class Test
        {
            public void Foo()
            {
                try
                {
                    try
                    {
                        throw new InvalidOperationException();
                    }
                    catch (ArgumentNullException)
                    {
                        // Handle ArgumentNullException
                    }
                }
                catch (IOException)
                {
                    // Handle IOException
                }
            }
        }
        """;

        var expected = Verifier.Diagnostic("THROW001")
            .WithSpan(12, 17, 12, 55)
            .WithArguments("InvalidOperationException");

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }

}
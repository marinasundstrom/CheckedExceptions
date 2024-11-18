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

    [Fact]
    public async Task Should_ReportDiagnostic_ForInvalidOperationException_NotCaught_InNestedTryCatch()
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
                        throw new IOException();
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

    [Fact]
    public async Task Should_ReportDiagnostic_ForIOException_ThrownInCatchBlock()
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
                        throw new IOException();
                    }
                }
                catch (InvalidOperationException)
                {
                    // Handle InvalidOperationException
                }
            }
        }
        """;

        var expected = Verifier.Diagnostic("THROW001")
            .WithSpan(16, 17, 16, 41)
            .WithArguments("IOException");

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }


    [Fact]
    public async Task Should_ReportDiagnostics_ForExceptions_ThrownInCatchAndFinallyBlocks()
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
                        throw new IOException();
                    }
                    finally 
                    {
                        throw new FormatException();
                    }
                }
                catch (InvalidOperationException)
                {
                    // Handle InvalidOperationException
                }
            }
        }
        """;

        var expected1 = Verifier.Diagnostic("THROW001")
            .WithSpan(16, 17, 16, 41)
            .WithArguments("IOException");

        var expected2 = Verifier.Diagnostic("THROW001")
            .WithSpan(20, 17, 20, 45)
            .WithArguments("FormatException");

        await Verifier.VerifyAnalyzerAsync(test, expected1, expected2);
    }

    [Fact]
    public async Task Should_ReportDiagnostic_ForIOException_Unhandled_InCatch_WithFinally_CatchingFormatException()
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
                        throw new IOException();
                    }
                    finally 
                    {
                        throw new FormatException();
                    }
                }
                catch (InvalidOperationException)
                {
                    // Handle InvalidOperationException
                }
                catch (FormatException)
                {
                    // Handle FormatException
                }
            }
        }
        """;

        var expected = Verifier.Diagnostic("THROW001")
            .WithSpan(16, 17, 16, 41)
            .WithArguments("IOException");

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }
}
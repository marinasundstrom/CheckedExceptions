using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

namespace Sundstrom.CheckedExceptions.Tests;

using Verifier = CSharpAnalyzerVerifier<CheckedExceptionsAnalyzer, DefaultVerifier>;

public partial class TryCatchTest
{
    [Fact]
    public async Task Should_NotReportDiagnosticForSingleExceptionInNestedTry()
    {
        var test = /* lang=c#-test */ """
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
        var test = /* lang=c#-test */ """
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
        var test = /* lang=c#-test */ """
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

        var expected = Verifier.IsThrown("InvalidOperationException")
            .WithSpan(12, 17, 12, 55); ;

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task Should_ReportDiagnostic_ForInvalidOperationException_NotCaught_InNestedTryCatch()
    {
        var test = /* lang=c#-test */ """
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

        var expected = Verifier.IsThrown("InvalidOperationException")
            .WithSpan(12, 17, 12, 55);

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task Should_ReportDiagnostic_ForIOException_ThrownInCatchBlock()
    {
        var test = /* lang=c#-test */ """
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

        var expected = Verifier.IsThrown("IOException")
            .WithSpan(16, 17, 16, 41);

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }


    [Fact]
    public async Task Should_ReportDiagnostics_ForExceptions_ThrownInCatchAndFinallyBlocks()
    {
        var test = /* lang=c#-test */ """
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

        var expected1 = Verifier.IsThrown("IOException")
            .WithSpan(16, 17, 16, 41);

        var expected2 = Verifier.IsThrown("FormatException")
            .WithSpan(20, 17, 20, 45);

        await Verifier.VerifyAnalyzerAsync(test, expected1, expected2);
    }

    [Fact]
    public async Task Should_ReportDiagnostic_ForIOException_Unhandled_InCatch_WithFinally_CatchingFormatException()
    {
        var test = /* lang=c#-test */ """
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

        var expected = Verifier.IsThrown("IOException")
            .WithSpan(16, 17, 16, 41);

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }
}

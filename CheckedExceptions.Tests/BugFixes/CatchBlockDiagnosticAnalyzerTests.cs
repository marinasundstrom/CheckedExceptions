using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

namespace Sundstrom.CheckedExceptions.Tests.BugFixes.CatchBlockHandling;

using Verifier = CSharpAnalyzerVerifier<CheckedExceptionsAnalyzer, DefaultVerifier>;

public partial class CatchBlockDiagnosticAnalyzerTests
{
    /// <summary>
    /// Ensures diagnostics are reported for unhandled exceptions 
    /// in both the try and catch blocks.
    /// </summary>
    [Fact]
    public async Task Should_ReportDiagnosticForUnhandledExceptionInTryAndCatchBlocks()
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
                        Console.Write("Foo");
                    }
                    catch (InvalidOperationException e)
                    {
                        Console.WriteLine(e.Message);
                    }
                }
            }
            """;

        var expected1 = Verifier.MightBeThrown("IOException")
            .WithSpan(10, 13, 10, 33); // Position of Console.Write("Foo");

        var expected2 = Verifier.MightBeThrown("IOException")
            .WithSpan(14, 13, 14, 41); // Position of Console.WriteLine(e.Message);

        await Verifier.VerifyAnalyzerAsync(test, [expected1, expected2]);
    }

    /// <summary>
    /// Ensures an unhandled exception in one catch block does not interfere 
    /// with the analysis of an adjacent catch block.
    /// </summary>
    [Fact]
    public async Task Should_ReportDiagnosticForUnhandledExceptionWithoutInterferingWithAdjacentCatch()
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
                        Console.Write("Foo");
                    }
                    catch (IOException e) { }
                    catch (InvalidOperationException e)
                    {
                        Console.WriteLine(e.Message);
                    }
                }
            }
            """;

        var expected = Verifier.MightBeThrown("IOException")
            .WithSpan(15, 13, 15, 41); // Position of Console.WriteLine(e.Message);

        await Verifier.VerifyAnalyzerAsync(test, [expected]);
    }
}
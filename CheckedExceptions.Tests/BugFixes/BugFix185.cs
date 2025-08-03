using Microsoft.CodeAnalysis.Testing;

namespace Sundstrom.CheckedExceptions.Tests.BugFixes;

using Verifier = CSharpAnalyzerVerifier<CheckedExceptionsAnalyzer, DefaultVerifier>;

public partial class Bugfix185
{
    [Fact]
    public async Task Test()
    {
        var test = /* lang=c#-test */ """
        using System;

        public class Test
        {
            [Throws(typeof(InvalidOperationException))]
            public static void NewMethod()
            {
                try 
                {
                    throw new ArgumentException("");
                }
                catch(InvalidOperationException ex) 
                {
                    throw new InvalidOperationException();      
                }
            }
        }
        """;

        var expected1 = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdRedundantExceptionDeclaration)
            .WithArguments("InvalidOperationException")
            .WithSpan(5, 20, 5, 45);

        var expected2 = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdUnhandled)
            .WithArguments("ArgumentException")
            .WithSpan(10, 13, 10, 45);

        await Verifier.VerifyAnalyzerAsync(test, opt =>
        {
            opt.ExpectedDiagnostics.AddRange(expected1, expected2);
            opt.DisabledDiagnostics.Remove(CheckedExceptionsAnalyzer.DiagnosticIdRedundantExceptionDeclaration);
        });
    }

    [Fact]
    public async Task Test_2()
    {
        var test = /* lang=c#-test */ """
        using System;

        public class Test
        {
            [Throws(typeof(InvalidOperationException))]
            public static void NewMethod()
            {
                try 
                {
                    throw new ArgumentException("");
                }
                catch(Exception ex) 
                {
                    throw new InvalidOperationException();      
                }
            }
        }
        """;

        await Verifier.VerifyAnalyzerAsync(test, opt =>
        {
            opt.DisabledDiagnostics.Remove(CheckedExceptionsAnalyzer.DiagnosticIdRedundantExceptionDeclaration);
        });
    }

    [Fact]
    public async Task Test2()
    {
        var test = /* lang=c#-test */ """
        using System;

        public class Test
        {
            [Throws(typeof(InvalidOperationException))]
            public static void NewMethod()
            {
                try 
                {
                    throw new ArgumentException("");
                }
                catch 
                {
                    throw new InvalidOperationException();      
                }
            }
        }
        """;

        await Verifier.VerifyAnalyzerAsync(test, opt =>
        {
            opt.DisabledDiagnostics.Remove(CheckedExceptionsAnalyzer.DiagnosticIdRedundantExceptionDeclaration);
        });
    }

    [Fact]
    public async Task Test3()
    {
        var test = /* lang=c#-test */ """
        using System;

        public class Test
        {
            [Throws(typeof(InvalidOperationException))]
            public static void NewMethod()
            {
                try 
                {

                }
                catch 
                {
                    throw new InvalidOperationException();      
                }
            }
        }
        """;

        var expected = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdRedundantCatchAllClause)
            .WithSpan(12, 9, 12, 14);

        await Verifier.VerifyAnalyzerAsync(test, opt =>
        {
            opt.ExpectedDiagnostics.AddRange(expected);
            opt.DisabledDiagnostics.Remove(CheckedExceptionsAnalyzer.DiagnosticIdRedundantExceptionDeclaration);
        });
    }

    [Fact]
    public async Task Test4()
    {
        var test = /* lang=c#-test */ """
            using System;

            ReadAndParse();

            [Throws(typeof(InvalidUserInputException))] // ✔️ Only the domain-specific exception is exposed
            static int ReadAndParse()
            {
                string input = "abc";  // Simulated input — could be user input in real scenarios

                try
                {
                    return int.Parse(input);
                }
                catch
                {
                    // Handle and rethrow as domain-specific exception
                    throw new InvalidUserInputException("Input was not a valid number.", null);
                }
            }

            class InvalidUserInputException : Exception
            {
                public InvalidUserInputException(string message, Exception inner)
                    : base(message, inner) { }
            }
        """;

        var expected = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdUnhandled)
            .WithArguments("InvalidUserInputException")
            .WithSpan(3, 5, 3, 19);

        await Verifier.VerifyAnalyzerAsync(test, opt =>
        {
            opt.ExpectedDiagnostics.Add(expected);
            opt.DisabledDiagnostics.Remove(CheckedExceptionsAnalyzer.DiagnosticIdRedundantExceptionDeclaration);
        }, executable: true);
    }
}
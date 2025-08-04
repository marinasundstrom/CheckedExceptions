using Microsoft.CodeAnalysis.Testing;

namespace Sundstrom.CheckedExceptions.Tests;

using Verifier = CSharpAnalyzerVerifier<CheckedExceptionsAnalyzer, DefaultVerifier>;

public partial class TryCatchTest2
{
    [Fact]
    public async Task Test()
    {
        var test = /* lang=c#-test */ """
        using System;

        Foo();

        [Throws(typeof(ArgumentException))]
        void Foo() 
        {
            try 
            {
                throw new InvalidOperationException();
            }
            catch(InvalidOperationException e) 
            {
            
            }

            throw new ArgumentException();
        }
        """;

        var expected = Verifier.UnhandledException("ArgumentException")
            .WithSpan(3, 1, 3, 6);

        await Verifier.VerifyAnalyzerAsync(test, setup: o =>
        {
            o.ExpectedDiagnostics.Add(expected);

            o.DisabledDiagnostics.Remove(CheckedExceptionsAnalyzer.DiagnosticIdRedundantExceptionDeclaration);
        }, executable: true);
    }

    [Fact]
    public async Task ArgumentException_IsRedundant_WhenThrowIsUnreachable()
    {
        var test = /* lang=c#-test */ """
    #nullable enable
    using System;

    class InvalidUserInputException : Exception 
    {
        public InvalidUserInputException(string m, Exception inner) : base(m, inner) { }
    }

    class C
    {
        [Throws(typeof(InvalidUserInputException), typeof(ArgumentException))]
        static int ReadAndParse()
        {
            string input = "abc";

            try
            {
                return int.Parse(input);
            }
            catch (FormatException ex)
            {
                throw new InvalidUserInputException("Input was not a valid number.", ex);
            }
            catch (OverflowException ex)
            {
                throw new InvalidUserInputException("Input number was too large.", ex);
            }

            throw new ArgumentException(); // <- unreachable
        }
    }
    """;

        var expected = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdRedundantExceptionDeclaration)
            .WithArguments("ArgumentException")
            .WithSpan(11, 55, 11, 72);

        await Verifier.VerifyAnalyzerAsync(test, o =>
        {
            o.ExpectedDiagnostics.Add(expected);

            o.DisabledDiagnostics.Remove(CheckedExceptionsAnalyzer.DiagnosticIdRedundantExceptionDeclaration);
        });
    }


    [Fact]
    public async Task ArgumentException_IsRedundant_WhenThrowIsUnreachable2()
    {
        var test = /* lang=c#-test */ """
    #nullable enable
    using System;

    class InvalidUserInputException : Exception 
    {
        public InvalidUserInputException(string m, Exception inner) : base(m, inner) { }
    }

    class C
    {
        [Throws(typeof(InvalidUserInputException), typeof(ArgumentException))]
        static int ReadAndParse()
        {
            string input = "abc";

            try
            {
                return int.Parse(input);
            }
            catch (FormatException ex)
            {
                throw new InvalidUserInputException("Input was not a valid number.", ex);
            }
            catch (OverflowException ex)
            {
                throw new InvalidUserInputException("Input number was too large.", ex);
            }
            catch (InvalidOperationException ex)
            {

            }

            throw new ArgumentException(); // <- unreachable
        }
    }
    """;

        var expected = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdRedundantExceptionDeclaration)
            .WithArguments("ArgumentException")
            .WithSpan(11, 55, 11, 72);

        await Verifier.VerifyAnalyzerAsync(test, o =>
        {
            o.ExpectedDiagnostics.Add(expected);

            o.DisabledDiagnostics.Remove(CheckedExceptionsAnalyzer.DiagnosticIdRedundantExceptionDeclaration);
        });
    }


    [Fact]
    public async Task ArgumentException_IsRedundant_WhenThrowIsUnreachable3()
    {
        var test = /* lang=c#-test */ """
    #nullable enable
    using System;

    class InvalidUserInputException : Exception 
    {
        public InvalidUserInputException(string m, Exception inner) : base(m, inner) { }
    }

    class C
    {
        [Throws(typeof(InvalidUserInputException), typeof(ArgumentException))]
        static int ReadAndParse()
        {
            string input = "abc";

            try
            {
                return int.Parse(input);
            }
            catch (FormatException ex)
            {
                throw new InvalidUserInputException("Input was not a valid number.", ex);
            }
            catch (OverflowException ex)
            {
                throw new InvalidUserInputException("Input number was too large.", ex);
            }
            catch
            {
                
            }

            throw new ArgumentException(); // <- unreachable
        }
    }
    """;

        var expected = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdRedundantExceptionDeclaration)
            .WithArguments("ArgumentException")
            .WithSpan(11, 55, 11, 72);

        var expected2 = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdRedundantCatchAllClause)
            .WithArguments("ArgumentException")
            .WithSpan(28, 9, 28, 14);

        await Verifier.VerifyAnalyzerAsync(test, o =>
        {
            o.ExpectedDiagnostics.AddRange(expected, expected2);

            o.DisabledDiagnostics.Remove(CheckedExceptionsAnalyzer.DiagnosticIdRedundantExceptionDeclaration);
        });
    }

    [Fact]
    public async Task ArgumentException_IsRedundant_WhenThrowIsUnreachable4()
    {
        var test = /* lang=c#-test */ """
    #nullable enable
    using System;

    class InvalidUserInputException : Exception 
    {
        public InvalidUserInputException(string m, Exception inner) : base(m, inner) { }
    }

    class C
    {
        [Throws(typeof(InvalidUserInputException), typeof(ArgumentException))]
        static int ReadAndParse()
        {
            string input = "abc";

            try
            {
                if (true)
                {
                    //May throw FormatException, or OverflowException, or return
                    return int.Parse(input);
                }            
            }
            catch (FormatException ex)
            {
                throw new InvalidUserInputException("Input was not a valid number.", ex);
            }
            catch (OverflowException ex)
            {
                throw new InvalidUserInputException("Input number was too large.", ex);
            }
            catch
            {
                
            }

            throw new ArgumentException(); // <- unreachable
        }
    }
    """;

        var expected = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdRedundantExceptionDeclaration)
            .WithArguments("ArgumentException")
            .WithSpan(11, 55, 11, 72);

        var expected2 = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdRedundantCatchAllClause)
            .WithArguments("ArgumentException")
            .WithSpan(32, 9, 32, 14);

        await Verifier.VerifyAnalyzerAsync(test, o =>
        {
            o.ExpectedDiagnostics.AddRange(expected, expected2);

            o.DisabledDiagnostics.Remove(CheckedExceptionsAnalyzer.DiagnosticIdRedundantExceptionDeclaration);
        });
    }

    [Fact]
    public async Task ArgumentException_IsRedundant_WhenThrowIsUnreachable5()
    {
        var test = /* lang=c#-test */ """
    #nullable enable
    using System;

    class InvalidUserInputException : Exception 
    {
        public InvalidUserInputException(string m, Exception inner) : base(m, inner) { }
    }

    class C
    {
        [Throws(typeof(InvalidUserInputException), typeof(ArgumentException))]
        static int ReadAndParse()
        {
            string input = "abc";

            try
            {
                bool x = true;
                if (x)
                {
                    //May throw FormatException, or OverflowException, or return
                    return int.Parse(input);
                }            
            }
            catch (FormatException ex)
            {
                throw new InvalidUserInputException("Input was not a valid number.", ex);
            }
            catch (OverflowException ex)
            {
                throw new InvalidUserInputException("Input number was too large.", ex);
            }
            catch
            {
                
            }

            throw new ArgumentException(); // <- unreachable
        }
    }
    """;

        var expected = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdRedundantCatchAllClause)
            .WithArguments("ArgumentException")
            .WithSpan(33, 9, 33, 14);

        await Verifier.VerifyAnalyzerAsync(test, o =>
        {
            o.ExpectedDiagnostics.AddRange(expected);

            o.DisabledDiagnostics.Remove(CheckedExceptionsAnalyzer.DiagnosticIdRedundantExceptionDeclaration);
        });
    }


    [Fact]
    public async Task ArgumentException_IsRedundant_WhenThrowIsUnreachable6()
    {
        var test = /* lang=c#-test */ """
    #nullable enable
    using System;

    class InvalidUserInputException : Exception 
    {
        public InvalidUserInputException(string m, Exception inner) : base(m, inner) { }
    }

    class C
    {
        [Throws(typeof(InvalidUserInputException), typeof(ArgumentException))]
        static int ReadAndParse()
        {
            string input = "abc";

            try
            {
                //May throw FormatException, or OverflowException, or return
                return int.Parse(input);    
            }
            catch (FormatException ex)
            {
                throw new InvalidUserInputException("Input was not a valid number.", ex);
            }
            catch (OverflowException ex)
            {
                throw new InvalidUserInputException("Input number was too large.", ex);
            }
            catch
            {
                
            }

            throw new ArgumentException(); // <- unreachable
        }
    }
    """;

        var expected = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdRedundantExceptionDeclaration)
            .WithArguments("ArgumentException")
            .WithSpan(11, 55, 11, 72);

        var expected2 = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdRedundantCatchAllClause)
            .WithArguments("ArgumentException")
            .WithSpan(29, 9, 29, 14);

        await Verifier.VerifyAnalyzerAsync(test, o =>
        {
            o.ExpectedDiagnostics.AddRange(expected, expected2);

            o.DisabledDiagnostics.Remove(CheckedExceptionsAnalyzer.DiagnosticIdRedundantExceptionDeclaration);
        });
    }

    [Fact]
    public async Task ArgumentException_IsRedundant_WhenThrowIsUnreachable7()
    {
        var test = /* lang=c#-test */ """
    #nullable enable
    using System;

    class InvalidUserInputException : Exception 
    {
        public InvalidUserInputException(string m, Exception inner) : base(m, inner) { }
    }

    class C
    {
        [Throws(typeof(InvalidUserInputException), typeof(ArgumentException))]
        static int ReadAndParse()
        {
            string input = "abc";

            try
            {
                bool x = true;
                while (false)
                {
                    //May throw FormatException, or OverflowException, or return
                    return int.Parse(input);
                }       
            }
            catch (FormatException ex)
            {
                throw new InvalidUserInputException("Input was not a valid number.", ex);
            }
            catch (OverflowException ex)
            {
                throw new InvalidUserInputException("Input number was too large.", ex);
            }
            catch
            {
                
            }

            throw new ArgumentException(); // <- unreachable
        }
    }
    """;

        var expected = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdRedundantCatchAllClause)
            .WithArguments("ArgumentException")
            .WithSpan(33, 9, 33, 14);

        await Verifier.VerifyAnalyzerAsync(test, o =>
        {
            o.ExpectedDiagnostics.AddRange(expected);

            o.DisabledDiagnostics.Remove(CheckedExceptionsAnalyzer.DiagnosticIdRedundantExceptionDeclaration);
        });
    }

    [Fact]
    public async Task Test100()
    {
        var test = /* lang=c#-test */ """
        using System;

        Foo();

        [Throws(typeof(ArgumentException))]
        void Foo()
        {
            try
            {
                MethodThatThrows2();
                MethodThatThrows();
            }
            catch
            {
                throw new OverflowException();
            }
        }

        [Throws(typeof(InvalidOperationException))]
        void MethodThatThrows()
        {
            throw new InvalidOperationException();
        }

        [Throws(typeof(ArgumentException))]
        void MethodThatThrows2()
        {
            throw new ArgumentException();
        }
        """;

        var expected = Verifier.UnhandledException("ArgumentException")
            .WithSpan(3, 1, 3, 6);

        var expected2 = Verifier.UnhandledException("OverflowException")
            .WithSpan(15, 9, 15, 39);

        var expected3 = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdRedundantExceptionDeclaration)
            .WithArguments("ArgumentException")
            .WithSpan(5, 16, 5, 33);

        await Verifier.VerifyAnalyzerAsync(test, setup: o =>
        {
            o.ExpectedDiagnostics.AddRange(expected, expected2, expected3);

            o.DisabledDiagnostics.Remove(CheckedExceptionsAnalyzer.DiagnosticIdRedundantExceptionDeclaration);
        }, executable: true);
    }
}
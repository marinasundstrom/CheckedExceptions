using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

namespace Sundstrom.CheckedExceptions.Tests;

using Verifier = CSharpAnalyzerVerifier<CheckedExceptionsAnalyzer, DefaultVerifier>;

partial class CheckedExceptionsAnalyzerTests
{
    [Fact]
    public async Task RedundantThrows_WhenNeverThrown()
    {
        var test = /* lang=c#-test */ """
            namespace Test;
            
            using System;

            public class ThrowsTest
            {
                [Throws(typeof(InvalidOperationException))]
                public void Foo()
                {
                }
            }
            """;

        var expected = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdRedundantExceptionDeclaration)
            .WithArguments("InvalidOperationException")
            .WithSpan(7, 20, 7, 45);

        await Verifier.VerifyAnalyzerAsync(test, o =>
        {
            ApplyDefaultOptions(o, expected);
        });
    }

    private void ApplyDefaultOptions(Verifier.AnalyzerTest options, params IEnumerable<DiagnosticResult> diagnosticResults)
    {
        options.ExpectedDiagnostics.AddRange(diagnosticResults);
        options.DisabledDiagnostics.Remove(CheckedExceptionsAnalyzer.DiagnosticIdRedundantExceptionDeclaration);
    }

    [Fact]
    public async Task NoDiagnostic_WhenExceptionActuallyThrown()
    {
        var test = /* lang=c#-test */ """
            namespace Test;
            
            using System;

            public class ThrowsTest
            {
                [Throws(typeof(InvalidOperationException))]
                public void Foo()
                {
                    throw new InvalidOperationException();
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task NoDiagnostic_WhenThrownInsideTryCatch()
    {
        var test = /* lang=c#-test */ """
            namespace Test;
            
            using System;

            public class ThrowsTest
            {
                [Throws(typeof(InvalidOperationException))]
                public void Foo()
                {
                    try
                    {
                        throw new InvalidOperationException();
                    }
                    catch (Exception)
                    {
                    }
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task NoDiagnostic_WhenThrownFromCatchBlock()
    {
        var test = /* lang=c#-test */ """
            namespace Test;
            
            using System;

            public class ThrowsTest
            {
                [Throws(typeof(InvalidOperationException))]
                public void Foo()
                {
                    try
                    {
                    }
                    catch (Exception)
                    {
                        throw new InvalidOperationException();
                    }
                }
            }
            """;

        var expected = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdRedundantCatchClause)
            .WithSpan(13, 9, 13, 14);

        await Verifier.VerifyAnalyzerAsync(test, o =>
        {
            o.ExpectedDiagnostics.Add(expected);

            o.DisabledDiagnostics.Remove(CheckedExceptionsAnalyzer.DiagnosticIdRedundantCatchClause);
        });
    }

    [Fact]
    public async Task RedundantThrows_WhenWrongExceptionDeclared()
    {
        var test = /* lang=c#-test */ """
            namespace Test;
            
            using System;

            public class ThrowsTest
            {
                [Throws(typeof(InvalidCastException))]
                public void Foo()
                {
                    throw new InvalidOperationException();
                }
            }
            """;

        var expected = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdRedundantExceptionDeclaration)
            .WithArguments("InvalidCastException")
            .WithSpan(7, 20, 7, 40);

        await Verifier.VerifyAnalyzerAsync(test, o =>
        {
            o.DisabledDiagnostics.Add(CheckedExceptionsAnalyzer.DiagnosticIdUnhandled);

            ApplyDefaultOptions(o, expected);
        });
    }

    [Fact]
    public async Task NoDiagnostic_WhenExceptionThrownInNestedTryCatch()
    {
        var test = /* lang=c#-test */ """
            namespace Test;

            using System;

            public class ThrowsTest
            {
                [Throws(typeof(InvalidOperationException))]
                public void Foo()
                {
                    try
                    {
                        try
                        {
                            throw new InvalidOperationException();
                        }
                        catch (ArgumentException)
                        {
                            // swallowed
                        }
                    }
                    catch (Exception)
                    {
                        // outer catch still swallows everything
                    }
                }
            }
            """;

        var expected = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdRedundantCatchClause)
            .WithSpan(16, 13, 16, 18);

        await Verifier.VerifyAnalyzerAsync(test, o =>
        {
            o.ExpectedDiagnostics.Add(expected);

            o.DisabledDiagnostics.Remove(CheckedExceptionsAnalyzer.DiagnosticIdRedundantCatchClause);
        });
    }

    [Fact]
    public async Task RedundantThrows_WhenInnerCatchHandlesException()
    {
        var test = /* lang=c#-test */ """
            namespace Test;

            using System;

            public class ThrowsTest
            {
                [Throws(typeof(InvalidOperationException))]
                public void Foo()
                {
                    try
                    {
                        throw new InvalidOperationException();
                    }
                    catch (InvalidOperationException)
                    {
                        // handled here, so does not escape
                    }
                }
            }
            """;

        var expected = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdRedundantExceptionDeclaration)
            .WithArguments("InvalidOperationException")
            .WithSpan(7, 20, 7, 45);

        await Verifier.VerifyAnalyzerAsync(test, o =>
        {
            ApplyDefaultOptions(o, expected);
        });
    }

    [Fact]
    public async Task NoDiagnostic_WhenCallingMethodThatDeclaresThrows()
    {
        var test = /* lang=c#-test */ """
            namespace Test;

            using System;

            public class Other
            {
                [Throws(typeof(InvalidOperationException))]
                public void Dangerous() { }
            }

            public class ThrowsTest
            {
                [Throws(typeof(InvalidOperationException))]
                public void Foo()
                {
                    var o = new Other();
                    o.Dangerous();
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task RedundantThrows_WhenCallingMethodThatDeclaresDifferentException()
    {
        var test = /* lang=c#-test */ """
            namespace Test;

            using System;

            public class Other
            {
                [Throws(typeof(InvalidCastException))]
                public void Dangerous() { }
            }

            public class ThrowsTest
            {
                [Throws(typeof(InvalidOperationException))]
                public void Foo()
                {
                    var o = new Other();
                    o.Dangerous();
                }
            }
            """;

        var expected1 = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdRedundantExceptionDeclaration)
            .WithArguments("InvalidCastException")
            .WithSpan(7, 20, 7, 40);

        var expected2 = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdRedundantExceptionDeclaration)
            .WithArguments("InvalidOperationException")
            .WithSpan(13, 20, 13, 45);

        await Verifier.VerifyAnalyzerAsync(test, o =>
        {
            o.DisabledDiagnostics.Add(CheckedExceptionsAnalyzer.DiagnosticIdUnhandled);

            ApplyDefaultOptions(o, expected1, expected2);
        });
    }

    [Fact]
    public async Task NoDiagnostic_WhenPropertyGetterHasThrows()
    {
        var test = /* lang=c#-test */ """
            namespace Test;

            using System;

            public class Other
            {
                [Throws(typeof(InvalidOperationException))]
                public int DangerousProp => throw new InvalidOperationException();
            }

            public class ThrowsTest
            {
                [Throws(typeof(InvalidOperationException))]
                public int Foo()
                {
                    var o = new Other();
                    return o.DangerousProp;
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task MixedThrowsAttributes_SomeRedundant()
    {
        var test = /* lang=c#-test */ """
            namespace Test;

            using System;

            public class ThrowsTest
            {
                [Throws(typeof(InvalidOperationException))]
                [Throws(typeof(ArgumentException))]
                [Throws(typeof(InvalidCastException))]
                public void Foo()
                {
                    // actually throws InvalidOperationException
                    throw new InvalidOperationException();
                }
            }
            """;

        var expected1 = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdRedundantExceptionDeclaration)
            .WithArguments("ArgumentException")
            .WithSpan(8, 20, 8, 37);

        var expected2 = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdRedundantExceptionDeclaration)
            .WithArguments("InvalidCastException")
            .WithSpan(9, 20, 9, 40);

        await Verifier.VerifyAnalyzerAsync(test, o =>
        {
            o.DisabledDiagnostics.Add(CheckedExceptionsAnalyzer.DiagnosticIdUnhandled);

            ApplyDefaultOptions(o, expected1, expected2);
        });
    }

    [Fact]
    public async Task RedundantThrows_WhenNeverThrown_LocalFunction()
    {
        var test = /* lang=c#-test */ """            
            using System;

            Foo();

            [Throws(typeof(InvalidOperationException))]
            void Foo()
            {
            }
            """;

        var expected = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdRedundantExceptionDeclaration)
            .WithArguments("InvalidOperationException")
            .WithSpan(5, 16, 5, 41);

        await Verifier.VerifyAnalyzerAsync(test, o =>
        {
            o.TestState.OutputKind = OutputKind.ConsoleApplication;

            o.DisabledDiagnostics.Add(CheckedExceptionsAnalyzer.DiagnosticIdUnhandled);

            ApplyDefaultOptions(o, expected);
        });
    }

    [Fact]
    public async Task RedundantThrows_WhenNeverThrown_LambdaFunction()
    {
        var test = /* lang=c#-test */ """            
            using System;

            foo();

            var f = [Throws(typeof(InvalidOperationException))] int () => 
            {
            
            }
            """;

        var expected = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdRedundantExceptionDeclaration)
            .WithArguments("InvalidOperationException")
            .WithSpan(5, 24, 5, 49);

        await Verifier.VerifyAnalyzerAsync(test, o =>
        {
            o.TestState.OutputKind = OutputKind.ConsoleApplication;
            o.CompilerDiagnostics = CompilerDiagnostics.None;

            o.DisabledDiagnostics.Add(CheckedExceptionsAnalyzer.DiagnosticIdUnhandled);

            ApplyDefaultOptions(o, expected);
        });
    }

    [Fact]
    public async Task RedundantThrows_WhenNeverThrown_LambdaFunction_ExpressionBody()
    {
        var test = /* lang=c#-test */ """            
            using System;

            foo();

            var f = [Throws(typeof(InvalidOperationException))] int () => true;
            """;

        var expected = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdRedundantExceptionDeclaration)
            .WithArguments("InvalidOperationException")
            .WithSpan(5, 24, 5, 49);

        await Verifier.VerifyAnalyzerAsync(test, o =>
        {
            o.TestState.OutputKind = OutputKind.ConsoleApplication;
            o.CompilerDiagnostics = CompilerDiagnostics.None;

            o.DisabledDiagnostics.Add(CheckedExceptionsAnalyzer.DiagnosticIdUnhandled);

            ApplyDefaultOptions(o, expected);
        });
    }

    [Fact]
    public async Task RedundantThrows_WhenThrown_LambdaFunction_ExpressionBody_NotShown()
    {
        var test = /* lang=c#-test */ """            
            using System;

            foo();

            var f = [Throws(typeof(InvalidOperationException))] int () => throw new InvalidOperationException;
            """;

        await Verifier.VerifyAnalyzerAsync(test, o =>
        {
            o.TestState.OutputKind = OutputKind.ConsoleApplication;
            o.CompilerDiagnostics = CompilerDiagnostics.None;

            o.DisabledDiagnostics.Add(CheckedExceptionsAnalyzer.DiagnosticIdUnhandled);

            ApplyDefaultOptions(o);
        });
    }

    [Fact]
    public async Task OvershadowedCatchClause_ShouldBe_MarkedAsRedundant()
    {
        var test = /* lang=c#-test */ """            
            using System;

            try
            {
                Foo();
            }
            catch (InvalidOperationException exc)
            {
                throw new InvalidCastException();
            }
            catch (InvalidOperationException exc)
            {
                throw new InvalidCastException();
            }

            [Throws(typeof(InvalidOperationException))] 
            int Foo () => throw new InvalidOperationException;
            """;

        var expected = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdRedundantCatchClause)
            .WithArguments("InvalidOperationException")
            .WithSpan(11, 1, 11, 6);

        await Verifier.VerifyAnalyzerAsync(test, o =>
        {
            o.ExpectedDiagnostics.Add(expected);
            o.TestState.OutputKind = OutputKind.ConsoleApplication;
            o.CompilerDiagnostics = CompilerDiagnostics.None;

            o.DisabledDiagnostics.Add(CheckedExceptionsAnalyzer.DiagnosticIdUnhandled);
            o.DisabledDiagnostics.Remove(CheckedExceptionsAnalyzer.DiagnosticIdRedundantCatchClause);

            ApplyDefaultOptions(o);
        });
    }
}
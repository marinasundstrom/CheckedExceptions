using Microsoft.CodeAnalysis.Testing;

namespace Sundstrom.CheckedExceptions.Tests.BugFixes;

using Verifier = CSharpAnalyzerVerifier<CheckedExceptionsAnalyzer, DefaultVerifier>;

public class BugFix264
{
    // Rethrow without inner handling → should raise THROW001
    [Fact]
    public async Task Rethrow_NotHandled_RaisesUnhandledDiagnostic()
    {
        var test = /* lang=c#-test */ """
        using System;

        public class TestClass
        {
            public void Test1()
            {
                try
                {
                    Foo();
                }
                catch
                {
                    throw;
                }
            }

            [Throws(typeof(InvalidOperationException))]
            public void Foo()
            {
                throw new InvalidOperationException();
            }
        }
        """;

        var expected = Verifier.UnhandledException("InvalidOperationException")
            .WithSpan(13, 13, 13, 19);

        await Verifier.VerifyAnalyzerAsync(test, s =>
        {
            s.ExpectedDiagnostics.Add(expected);

            s.DisabledDiagnostics.Remove(CheckedExceptionsAnalyzer.DiagnosticIdRedundantTypedCatchClause);
            s.DisabledDiagnostics.Add(CheckedExceptionsAnalyzer.DiagnosticIdGeneralThrowDeclared);
        });
    }

    // Rethrow handled by inner typed catch → no diagnostic
    [Fact]
    public async Task Rethrow_HandledByInnerTypedCatch_NoDiagnostic()
    {
        var test = /* lang=c#-test */ """
        using System;

        public class TestClass
        {
            public void Test1()
            {
                try
                {
                    Foo();
                }
                catch
                {
                    try
                    {
                        throw;
                    }
                    catch (InvalidOperationException invalidOperationException)
                    {
                    }
                }
            }

            [Throws(typeof(InvalidOperationException))]
            public void Foo()
            {
                throw new InvalidOperationException();
            }
        }
        """;

        await Verifier.VerifyAnalyzerAsync(test, s =>
        {
            s.DisabledDiagnostics.Remove(CheckedExceptionsAnalyzer.DiagnosticIdRedundantTypedCatchClause);
            s.DisabledDiagnostics.Add(CheckedExceptionsAnalyzer.DiagnosticIdGeneralThrowDeclared);
        });
    }

    // Rethrow handled by inner general catch → no diagnostic
    [Fact]
    public async Task Rethrow_HandledByInnerGeneralCatch_NoDiagnostic()
    {
        var test = /* lang=c#-test */ """
        using System;

        public class TestClass
        {
            public void Test1()
            {
                try
                {
                    Foo();
                }
                catch
                {
                    try
                    {
                        throw;
                    }
                    catch
                    {
                    }
                }
            }

            [Throws(typeof(InvalidOperationException))]
            public void Foo()
            {
                throw new InvalidOperationException();
            }
        }
        """;

        await Verifier.VerifyAnalyzerAsync(test, s =>
        {
            s.DisabledDiagnostics.Remove(CheckedExceptionsAnalyzer.DiagnosticIdRedundantTypedCatchClause);
            s.DisabledDiagnostics.Add(CheckedExceptionsAnalyzer.DiagnosticIdGeneralThrowDeclared);
        });
    }

    // Nested inner tries where only an outer inner-try catches → still handled → no diagnostic
    [Fact]
    public async Task Rethrow_HandledByOuterInnerTry_NoDiagnostic()
    {
        var test = /* lang=c#-test */ """
        using System;

        public class TestClass
        {
            public void Test1()
            {
                try
                {
                    Foo();
                }
                catch
                {
                    try
                    {
                        try
                        {
                            throw;
                        }
                        finally
                        {
                        }
                    }
                    catch (InvalidOperationException)
                    {
                    }
                }
            }

            [Throws(typeof(InvalidOperationException))]
            public void Foo()
            {
                throw new InvalidOperationException();
            }
        }
        """;

        await Verifier.VerifyAnalyzerAsync(test, s =>
        {
            s.DisabledDiagnostics.Remove(CheckedExceptionsAnalyzer.DiagnosticIdRedundantTypedCatchClause);
            s.DisabledDiagnostics.Add(CheckedExceptionsAnalyzer.DiagnosticIdGeneralThrowDeclared);
        });
    }
}
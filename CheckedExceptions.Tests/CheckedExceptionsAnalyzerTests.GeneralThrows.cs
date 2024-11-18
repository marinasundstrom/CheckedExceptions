using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

namespace CheckedExceptions.Tests;

using Verifier = CSharpAnalyzerVerifier<CheckedExceptionsAnalyzer, DefaultVerifier>;

partial class CheckedExceptionsAnalyzerTests
{
    [Fact]
    public async Task GeneralThrowsInPropertyGetterSetter_ShouldReportDiagnostic()
    {
        var test = /* lang=c#-test */ """
            using System;

            public class TestClass
            {
                public int Property
                {
                    [Throws(typeof(Exception))]
                    get
                    {
                        throw new Exception();
                    }

                    [Throws(typeof(Exception))]
                    set
                    {
                        throw new Exception();
                    }
                }
            }
            """;

        var expectedGetter = Verifier.Diagnostic("THROW004")
            .WithSpan(10, 13, 10, 35)
            .WithArguments("Exception");

        var expectedSetter = Verifier.Diagnostic("THROW004")
            .WithSpan(16, 13, 16, 35)
            .WithArguments("Exception");

        await Verifier.VerifyAnalyzerAsync(test, expectedGetter, expectedSetter);
    }

    [Fact]
    public async Task GeneralThrowsInEventAddRemove_ShouldReportDiagnostic()
    {
        var test = /* lang=c#-test */ """
        using System;

        public class TestClass
        {
            public event EventHandler MyEvent
            {
                [Throws(typeof(Exception))]
                add
                {
                    throw new Exception();
                }

                [Throws(typeof(Exception))]
                remove
                {
                    throw new Exception();
                }
            }
        }
        """;

        var expectedAdd = Verifier.Diagnostic("THROW004")
            .WithSpan(10, 13, 10, 35)
            .WithArguments("Exception");

        var expectedRemove = Verifier.Diagnostic("THROW004")
            .WithSpan(16, 13, 16, 35)
            .WithArguments("Exception");

        await Verifier.VerifyAnalyzerAsync(test, expectedAdd, expectedRemove);
    }


    [Fact]
    public async Task GeneralThrowsInLambda_ShouldReportDiagnostic()
    {
        var test = /* lang=c#-test */ """
            using System;

            public class TestClass
            {
                public void TestMethod()
                {
                    Action action = [Throws(typeof(Exception))] () => throw new Exception();
                    action();
                }
            }
            """;

        var expected = Verifier.Diagnostic("THROW001")
            .WithSpan(8, 9, 8, 17)
            .WithArguments("Exception");

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task GeneralThrowsInLocalFunction_ShouldReportDiagnostic()
    {
        var test = /* lang=c#-test */ """
            using System;

            public class TestClass
            {
                public void TestMethod()
                {
                    [Throws(typeof(Exception))]
                    void LocalFunction()
                    {
                        throw new Exception();
                    }

                    LocalFunction();
                }
            }
            """;

        var expected = Verifier.Diagnostic("THROW001")
            .WithSpan(13, 9, 13, 24)
            .WithArguments("Exception");

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }
}

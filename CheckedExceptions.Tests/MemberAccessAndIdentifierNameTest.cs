using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

namespace CheckedExceptions.Tests;

using Verifier = CSharpAnalyzerVerifier<CheckedExceptionsAnalyzer, DefaultVerifier>;

public partial class MemberAccessAndIdentifierNameTest
{
    [Fact]
    public async Task Should_ReportDiagnostic_When_AssigningInstanceProperty_ViaThisAccess()
    {
        var test = """
            using System;

            public class Test
            {
                public string Value 
                {
                    get;

                    [Throws(typeof(ArgumentNullException))]
                    set;
                }

                public void Foo()
                {
                    this.Value = "TEST";
                }
            }
            """;

        var expected = Verifier.Diagnostic("THROW001")
            .WithSpan(15, 9, 15, 19)
            .WithArguments("ArgumentNullException");

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task Should_ReportDiagnostic_When_AssigningInstanceProperty_Directly()
    {
        var test = """
            using System;

            public class Test
            {
                public string Value 
                {
                    get;

                    [Throws(typeof(ArgumentNullException))]
                    set;
                }

                public void Foo()
                {
                    Value = "TEST";
                }
            }
            """;

        var expected = Verifier.Diagnostic("THROW001")
            .WithSpan(15, 9, 15, 14)
            .WithArguments("ArgumentNullException");

        await Verifier.VerifyAnalyzerAsync(test, [expected]);
    }

    [Fact]
    public async Task Should_ReportDiagnostic_When_AssigningStaticProperty_ViaClassName()
    {
        var test = """
            using System;

            public class Test
            {
                public static string Value 
                {
                    get;

                    [Throws(typeof(ArgumentNullException))]
                    set;
                }

                public void Foo()
                {
                    Test.Value = "TEST";
                }
            }
            """;

        var expected = Verifier.Diagnostic("THROW001")
            .WithSpan(15, 9, 15, 19)
            .WithArguments("ArgumentNullException");

        await Verifier.VerifyAnalyzerAsync(test, [expected]);
    }
}
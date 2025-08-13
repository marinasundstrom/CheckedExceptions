using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;

namespace Sundstrom.CheckedExceptions.Tests;

using Verifier = CSharpAnalyzerVerifier<CheckedExceptionsAnalyzer, DefaultVerifier>;

public partial class XmlDocTest
{
    // Test 2: Validates multiple exceptions declared in XML documentation for int.Parse
    [Fact(DisplayName = "Multiple exceptions declared in XML documentation for int.Parse should not trigger diagnostics")]
    public async Task MultipleDeclaredExceptionsForIntParse_ShouldNotReportDiagnostic()
    {
        var test = /* lang=c#-test */ """
            using System;

            public class TestClass
            {
                public void TestMethod1()
                {
                    int.Parse("42");
                }
            }
            """;

        var expected1 = Verifier.UnhandledException("ArgumentNullException")
            .WithSpan(7, 13, 7, 24);

        var expected2 = Verifier.UnhandledException("FormatException")
            .WithSpan(7, 13, 7, 24);

        var expected3 = Verifier.UnhandledException("OverflowException")
            .WithSpan(7, 13, 7, 24);

        await Verifier.VerifyAnalyzerAsync(test, expected1, expected2, expected3);
    }


    // Test 3: Checks XML documentation with nullable enabled and verifies FormatException and OverflowException diagnostics
    [Fact(DisplayName = "With nullable enabled, declared FormatException and OverflowException should not trigger diagnostics")]
    public async Task NullableEnabled_WithDeclaredFormatAndOverflowExceptions_ShouldNotReportDiagnostic()
    {
        var test = /* lang=c#-test */ """
            #nullable enable
            using System;

            public class TestClass
            {
                public void TestMethod1()
                {
                    int.Parse("42");
                }
            }
            """;

        var expected2 = Verifier.UnhandledException("FormatException")
            .WithSpan(8, 13, 8, 24);

        var expected3 = Verifier.UnhandledException("OverflowException")
            .WithSpan(8, 13, 8, 24);

        await Verifier.VerifyAnalyzerAsync(test, [expected2, expected3]);
    }


    // Test 4: Ensures no diagnostics are reported when using StringBuilder without exception-related operations
    [Fact(DisplayName = "StringBuilder usage without exception-related operations should not trigger diagnostics")]
    public async Task StringBuilderUsageWithoutExceptions_ShouldNotReportDiagnostic()
    {
        var test = /* lang=c#-test */ """
            #nullable enable
            using System;
            using System.Text;

            public class TestClass
            {
                public void TestMethod1()
                {
                    StringBuilder stringBuilder = new StringBuilder();

                    var x = stringBuilder.Length;
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(test);
    }

    // Test 5: Verifies that setting an invalid Length on StringBuilder triggers ArgumentOutOfRangeException diagnostic
    [Fact(DisplayName = "Setting invalid Length on StringBuilder should trigger ArgumentOutOfRangeException diagnostic")]
    public async Task SettingInvalidLengthOnStringBuilder_ShouldReportArgumentOutOfRangeException()
    {
        var test = /* lang=c#-test */ """
            #nullable enable
            using System;
            using System.Text;

            public class TestClass
            {
                public void TestMethod1()
                {
                    StringBuilder stringBuilder = new StringBuilder();

                    stringBuilder.Length = 2;
                }
            }
            """;

        var expected = Verifier.UnhandledException("ArgumentOutOfRangeException")
            .WithSpan(11, 23, 11, 29);

        await Verifier.VerifyAnalyzerAsync(test, [expected]);
    }



}
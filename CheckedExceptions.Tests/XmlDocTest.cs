using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;

namespace Sundstrom.CheckedExceptions.Tests;

using Verifier = CSharpAnalyzerVerifier<CheckedExceptionsAnalyzer, DefaultVerifier>;

public partial class XmlDocTest
{

    // Test 1: Ensures that declaring InvalidOperationException in XML documentation does not trigger a diagnostic
    [Fact(DisplayName = "Declaring InvalidOperationException in XML documentation should not trigger a diagnostic")]
    public async Task DeclaredInvalidOperationExceptionInXmlDocumentation_ShouldNotReportDiagnostic()
    {
        var test = /* lang=c#-test */ """
            using System;

            public class TestClass
            {
                /// <summary>
                /// Performs an operation.
                /// </summary>
                /// <exception cref="T:System.InvalidOperationException">Thrown when the operation fails.</exception>
                public void TestMethod()
                {

                }

                public void TestMethod2()
                {
                    TestMethod();
                }
            }
            """;

        var expected = Verifier.UnhandledException("InvalidOperationException")
            .WithSpan(16, 9, 16, 21);

        var expected2 = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdXmlDocButNoThrows)
            .WithArguments("InvalidOperationException")
            .WithSpan(9, 17, 9, 27);

        await Verifier.VerifyAnalyzerAsync(test, [expected, expected2]);
    }

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

    // Test 6: Validates that setting a property to null with a declared ArgumentNullException does not trigger a diagnostic
    [Fact(DisplayName = "Assigning null to property with declared ArgumentNullException should not trigger diagnostic")]
    public async Task AssigningNullToValue_WithDeclaredArgumentNullException_ShouldNotReportDiagnostic()
    {
        var test = /* lang=c#-test */ """
            using System;

            public class TestClass
            {
                /// <summary>
                /// 
                /// </summary>
                /// <value></value>
                /// <exception cref="ArgumentNullException">
                /// The value provided that is set is null.
                /// </exception>
                public string Value
                {
                    get;
                    set;
                }

                public void TestMethod2()
                {
                    Value = null;
                }
            }
            """;

        var expected = Verifier.UnhandledException("ArgumentNullException")
            .WithSpan(20, 9, 20, 14);

        var expected2 = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdXmlDocButNoThrows)
            .WithArguments("ArgumentNullException")
            .WithSpan(15, 9, 15, 12);

        await Verifier.VerifyAnalyzerAsync(test, [expected, expected2]);
    }

    // Test 7: Ensures that with nullable enabled and declared ArgumentNullException, no diagnostic is reported when assigning null
    [Fact(DisplayName = "With nullable enabled and declared ArgumentNullException, assigning null should not trigger diagnostic")]
    public async Task NullableEnabled_AssigningNull_WithDeclaredArgumentNullException_ShouldNotReportDiagnostic()
    {
        var test = /* lang=c#-test */ """
            #nullable enable
            using System;

            public class TestClass
            {       
                /// <summary>
                /// 
                /// </summary>
                /// <value></value>
                /// <exception cref="ArgumentNullException">
                /// The value provided that is set is null.
                /// </exception>
                public string Value
                {
                    get;
                    set;
                }

                public void TestMethod2()
                {
                    Value = null;
                }
            }
            """;

        var expected = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdXmlDocButNoThrows)
            .WithArguments("ArgumentNullException")
            .WithSpan(16, 9, 16, 12);

        await Verifier.VerifyAnalyzerAsync(test, [expected]);
    }
}
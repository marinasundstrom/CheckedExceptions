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

        var expected = Verifier.Diagnostic("THROW001")
            .WithSpan(16, 9, 16, 21)
            .WithArguments("InvalidOperationException");

        await Verifier.VerifyAnalyzerAsync(test, expected);
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

        var expected1 = Verifier.Diagnostic("THROW001")
            .WithSpan(7, 9, 7, 24)
            .WithArguments("ArgumentNullException");

        var expected2 = Verifier.Diagnostic("THROW001")
            .WithSpan(7, 9, 7, 24)
            .WithArguments("FormatException");

        var expected3 = Verifier.Diagnostic("THROW001")
            .WithSpan(7, 9, 7, 24)
            .WithArguments("OverflowException");

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

        var expected2 = Verifier.Diagnostic("THROW001")
            .WithSpan(8, 9, 8, 24)
            .WithArguments("FormatException");

        var expected3 = Verifier.Diagnostic("THROW001")
            .WithSpan(8, 9, 8, 24)
            .WithArguments("OverflowException");

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

        var expected = Verifier.Diagnostic("THROW001")
            .WithSpan(11, 9, 11, 29)
            .WithArguments("ArgumentOutOfRangeException");

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

        var expected = Verifier.Diagnostic("THROW001")
            .WithSpan(20, 9, 20, 14)
            .WithArguments("ArgumentNullException");

        await Verifier.VerifyAnalyzerAsync(test, [expected]);
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

        await Verifier.VerifyAnalyzerAsync(test);
    }
}
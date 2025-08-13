using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;

namespace Sundstrom.CheckedExceptions.Tests;

using Verifier = CSharpAnalyzerVerifier<CheckedExceptionsAnalyzer, DefaultVerifier>;

public partial class XmlDocTest_MissingThrowsDeclaration
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

    [Fact()]
    public async Task Test()
    {
        var test = /* lang=c#-test */ """
            using System;

            public class TestClass
            {
                /// <summary>
                /// 
                /// </summary>
                /// <value></value>
                /// <exception cref="InvalidOperationException">
                /// When getting from an invalid state.
                /// </exception>
                /// <exception cref="ArgumentNullException">
                /// The value provided that is set is null.
                /// </exception>
                public string Value
                {
                    get;
                    set;
                }
            }
            """;

        var expected = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdXmlDocButNoThrows)
            .WithArguments("InvalidOperationException")
            .WithSpan(17, 9, 17, 12);

        var expected2 = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdXmlDocButNoThrows)
            .WithArguments("ArgumentNullException")
            .WithSpan(18, 9, 18, 12);

        await Verifier.VerifyAnalyzerAsync(test, o =>
        {
            o.ExpectedDiagnostics.AddRange(expected, expected2);

            o.DisabledDiagnostics.Remove(CheckedExceptionsAnalyzer.DiagnosticIdXmlDocButNoThrows);
        });
    }

    [Fact()]
    public async Task Test2()
    {
        var test = /* lang=c#-test */ """
            using System;

            public class TestClass
            {
                /// <exception cref="System.InvalidOperationException">
                /// When getting a value.
                /// </exception>
                public int Foo => 0;
            }
            """;

        var expected = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdXmlDocButNoThrows)
            .WithArguments("InvalidOperationException")
            .WithSpan(8, 16, 8, 19);

        await Verifier.VerifyAnalyzerAsync(test, o =>
        {
            o.ExpectedDiagnostics.Add(expected);

            o.DisabledDiagnostics.Remove(CheckedExceptionsAnalyzer.DiagnosticIdXmlDocButNoThrows);
        });
    }

    // Getter with BLOCK BODY; XML docs for getter → anchor on getter method
    [Fact(DisplayName = "Getter(block body) with <exception/> on property should report missing [Throws] anchored on getter")]
    public async Task Getter_BlockBody_DocOnProperty_ShouldReport_OnGetter()
    {
        var test = /* lang=c#-test */ """
            using System;

            public class C
            {
                /// <exception cref="InvalidOperationException">get</exception>
                public int P
                {
                    get
                    {
                        return 0;
                    }
                    set { }
                }
            }
            """;

        var expected = Verifier
            .Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdXmlDocButNoThrows)
            .WithArguments("InvalidOperationException")
            .WithSpan(8, 9, 8, 12);

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }

    // Setter with BLOCK BODY; XML docs for setter → anchor on setter method
    [Fact(DisplayName = "Setter(block body) with <exception/> on property should report missing [Throws] anchored on setter")]
    public async Task Setter_BlockBody_DocOnProperty_ShouldReport_OnSetter()
    {
        var test = /* lang=c#-test */ """
            using System;

            public class C
            {
                /// <exception cref="ArgumentNullException">set</exception>
                public string P
                {
                    get { return ""; }
                    set
                    {
                        _ = value.Length;
                    }
                }
            }
            """;

        var expected = Verifier
            .Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdXmlDocButNoThrows)
            .WithArguments("ArgumentNullException")
            .WithSpan(9, 9, 9, 12);

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }

    // Both accessors with BLOCK BODIES; ambiguous XML (neither getter nor setter phrasing) → anchor on property
    [Fact(DisplayName = "Getter+Setter(block bodies) with ambiguous <exception/> should anchor on property (single report)")]
    public async Task GetterAndSetter_BlockBodies_AmbiguousDoc_ShouldReport_OnProperty()
    {
        var test = /* lang=c#-test */ """
            using System;

            public class C
            {
                /// <exception cref="InvalidOperationException">Thrown by the property.</exception>
                public int P
                {
                    get { return 42; }
                    set { _ = value; }
                }
            }
            """;

        var expected = Verifier
            .Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdXmlDocButNoThrows)
            .WithArguments("InvalidOperationException")
            .WithSpan(8, 9, 8, 12);

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }

    // Expression-bodied GET accessor; docs for getter
    [Fact(DisplayName = "Getter(expression-bodied) with <exception/> should report anchored on getter")]
    public async Task Getter_ExpressionBodied_ShouldReport_OnGetter()
    {
        var test = /* lang=c#-test */ """
            using System;

            public class C
            {
                /// <exception cref="InvalidOperationException">get</exception>
                public int P
                {
                    get => 1 / int.Parse("1"); // body present, but location comes from symbol
                    set { }
                }
            }
            """;

        var expected = Verifier
            .Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdXmlDocButNoThrows)
            .WithArguments("InvalidOperationException")
            .WithSpan(8, 9, 8, 12);

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }

    // Expression-bodied SET accessor; docs for setter
    [Fact(DisplayName = "Setter(expression-bodied) with <exception/> should report anchored on setter")]
    public async Task Setter_ExpressionBodied_ShouldReport_OnSetter()
    {
        var test = /* lang=c#-test */ """
            using System;

            public class C
            {
                /// <exception cref="ArgumentNullException">set</exception>
                public string P
                {
                    get => "";
                    set => _ = value.Length;
                }
            }
            """;

        var expected = Verifier
            .Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdXmlDocButNoThrows)
            .WithArguments("ArgumentNullException")
            .WithSpan(9, 9, 9, 12);

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }

    // Mixed: auto GET; SET with BODY; docs for setter → should anchor on setter only
    [Fact(DisplayName = "Auto-getter + setter(body) with <exception/> for setter should report on setter only")]
    public async Task Mixed_AutoGetter_SetterBody_DocForSetter_ShouldReport_OnSetter()
    {
        var test = /* lang=c#-test */ """
            using System;

            public class C
            {
                /// <exception cref="ArgumentException">set</exception>
                public int P
                {
                    get;
                    set
                    {
                        if (value < 0) throw new ArgumentException();
                    }
                }
            }
            """;

        var expected = Verifier
            .Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdXmlDocButNoThrows)
            .WithArguments("ArgumentException")
            .WithSpan(9, 9, 9, 12);

        var expected2 = Verifier
            .UnhandledException("ArgumentException")
            .WithSpan(11, 28, 11, 58);

        await Verifier.VerifyAnalyzerAsync(test, setup: opt =>
        {
            opt.ExpectedDiagnostics.AddRange(expected, expected2);
            opt.CompilerDiagnostics = CompilerDiagnostics.None;
        });
    }

    // Expression-bodied PROPERTY (=>), i.e., no accessor list; docs on property → anchor on property
    [Fact(DisplayName = "Expression-bodied property with <exception/> should report anchored on property")]
    public async Task ExpressionBodiedProperty_ShouldReport_OnProperty()
    {
        var test = /* lang=c#-test */ """
            using System;

            public class C
            {
                /// <exception cref="InvalidOperationException">prop</exception>
                public int P => int.Parse("0"); // expression-bodied property
            }
            """;

        var expected = Verifier
            .Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdXmlDocButNoThrows)
            .WithArguments("InvalidOperationException")
            .WithSpan(6, 16, 6, 17);

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }

    // Guard against duplicate reports: doc lists one exception; ensure exactly one diagnostic is produced
    [Fact(DisplayName = "No duplicate diagnostics for accessor-with-body")]
    public async Task NoDuplicate_ForAccessorWithBody()
    {
        var test = /* lang=c#-test */ """
            using System;

            public class C
            {
                /// <exception cref="InvalidOperationException">get</exception>
                public int P
                {
                    get { return 0; }
                    set { }
                }
            }
            """;

        var expected = Verifier
            .Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdXmlDocButNoThrows)
            .WithArguments("InvalidOperationException")
            .WithSpan(8, 9, 8, 12);

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }
}
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;

namespace Sundstrom.CheckedExceptions.Tests;

using Verifier = CSharpAnalyzerVerifier<CheckedExceptionsAnalyzer, DefaultVerifier>;

public partial class XmlDocTest2
{
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
            .WithSpan(8, 23, 8, 24);

        await Verifier.VerifyAnalyzerAsync(test, o =>
        {
            o.ExpectedDiagnostics.Add(expected);

            o.DisabledDiagnostics.Remove(CheckedExceptionsAnalyzer.DiagnosticIdXmlDocButNoThrows);
        });
    }
}
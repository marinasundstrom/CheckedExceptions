using Microsoft.CodeAnalysis.Testing;

namespace Sundstrom.CheckedExceptions.Tests.BugFixes;

using Verifier = CSharpAnalyzerVerifier<CheckedExceptionsAnalyzer, DefaultVerifier>;

public class BugFix253
{
    [Fact]
    public async Task Test1()
    {
        var test = /* lang=c#-test */ """
        using System;

        public class TestClass
        {
            public void Test1()
            {
                try
                {
                    var test = bool (string s) =>
                    {
                        throw new InvalidOperationException();
                        return true;
                    };

                    test("");
                }
                catch (InvalidOperationException e)
                {

                }
            }
        }
        """;


        var expected = Verifier.UnhandledException("InvalidOperationException")
            .WithSpan(11, 17, 11, 55);

        var expected2 = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdRedundantTypedCatchClause)
            .WithArguments("InvalidOperationException")
            .WithSpan(17, 16, 17, 41);

        await Verifier.VerifyAnalyzerAsync(test, s =>
        {
            s.ExpectedDiagnostics.AddRange(expected, expected2);

            s.DisabledDiagnostics.Remove(CheckedExceptionsAnalyzer.DiagnosticIdRedundantTypedCatchClause);

            s.DisabledDiagnostics.Add(CheckedExceptionsAnalyzer.DiagnosticIdGeneralThrowDeclared);
        });
    }

    [Fact]
    public async Task Test2()
    {
        var test = /* lang=c#-test */ """
        using System;

        public class TestClass
        {
            public void Test1()
            {
                try
                {
                    var test = [Throws(typeof(InvalidOperationException))] bool (string s) =>
                    {
                        throw new InvalidOperationException();
                        return true;
                    };

                    test("");
                }
                catch (InvalidOperationException e)
                {

                }
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
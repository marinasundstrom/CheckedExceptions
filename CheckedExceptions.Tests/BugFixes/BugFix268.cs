using Microsoft.CodeAnalysis.Testing;

namespace Sundstrom.CheckedExceptions.Tests.BugFixes;

using Verifier = CSharpAnalyzerVerifier<CheckedExceptionsAnalyzer, DefaultVerifier>;

public class BugFix268
{
    [Fact]
    public async Task DeclarationShouldSilenceExceptionFromImplicitObjectCreation()
    {
        var test = /* lang=c#-test */ """
        using System;

        public class TestClass
        {
            [Throws(typeof(InvalidOperationException))]
            public void TargetTypedNew()
            {
                ThrowingObject obj = new(); // ❗ Expect diagnostic
            }
        }

        public class ThrowingObject
        {
            [Throws(typeof(InvalidOperationException))]
            public ThrowingObject()
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
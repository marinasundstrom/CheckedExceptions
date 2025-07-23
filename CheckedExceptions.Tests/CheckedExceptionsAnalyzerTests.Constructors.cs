using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

namespace Sundstrom.CheckedExceptions.Tests;

using Verifier = CSharpAnalyzerVerifier<CheckedExceptionsAnalyzer, DefaultVerifier>;

partial class CheckedExceptionsAnalyzerTests
{
    [Fact]
    public async Task ThrowingConstructor_WithThrowsAttribute_ShouldReportDiagnostics_WhenCallerDoesNotHandleOrDeclare()
    {
        var test = /* lang=c#-test */ """
        using System;

        public class ThrowingObject
        {
            [Throws(typeof(InvalidOperationException))]
            public ThrowingObject()
            {
                throw new InvalidOperationException();
            }
        }

        public class TestClass
        {
            public void ExplicitNew()
            {
                ThrowingObject obj = new ThrowingObject(); // ❗ Expect diagnostic
            }

            public void TargetTypedNew()
            {
                ThrowingObject obj = new(); // ❗ Expect diagnostic
            }
        }
        """;

        var expected1 = Verifier.UnhandledException("InvalidOperationException")
            .WithSpan(16, 30, 16, 50); // new ThrowingObject()

        var expected2 = Verifier.UnhandledException("InvalidOperationException")
            .WithSpan(21, 30, 21, 35); // new()

        await Verifier.VerifyAnalyzerAsync(test, expected1, expected2);
    }
}
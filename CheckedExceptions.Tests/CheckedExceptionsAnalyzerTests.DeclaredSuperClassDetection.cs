using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

namespace Sundstrom.CheckedExceptions.Tests;

using Verifier = CSharpAnalyzerVerifier<CheckedExceptionsAnalyzer, DefaultVerifier>;

partial class CheckedExceptionsAnalyzerTests
{
    [Fact]
    public async Task Verify_RedundantThrowsHandledByDeclaredSuperClass()
    {
        var test = /* lang=c#-test */ """
            using System;
            
            public class TestClass
            {
                [Throws(typeof(InvalidOperationException))]
                [Throws(typeof(ObjectDisposedException))]
                public void CallerMethod()
                {
                    
                }
            }
            """;

        var expected = Verifier.RedundantExceptionDeclaration("System.InvalidOperationException")
            .WithSpan(6, 13, 6, 44);

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }
}
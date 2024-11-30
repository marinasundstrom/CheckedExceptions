using Microsoft.CodeAnalysis.Testing;

namespace Sundstrom.CheckedExceptions.Tests.BugFixes;

using Verifier = CSharpAnalyzerVerifier<CheckedExceptionsAnalyzer, DefaultVerifier>;

public partial class Bugfix67_DuplicateDeclarationsOnLambda
{
    [Fact]
    public async Task SingleExceptionTypeInThrowsAttribute_ShouldNotReportDiagnostic()
    {
        var test = /* lang=c#-test */ """
        using System;
        using System.Collections.Generic;
        using System.Linq;

        public class ThrowTest
        {
            public void MethodThatThrows()
            {
               var f = [Throws(typeof(ArgumentNullException))] bool (string str) => { return true; };
            }
        }
        """;

        await Verifier.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task SameExceptionTypeDeclaredTwiceInThrowsAttribute_ShouldReportDiagnostic()
    {
        var test = /* lang=c#-test */ """
        using System;
        using System.Collections.Generic;
        using System.Linq;

        public class ThrowTest
        {
            public void MethodThatThrows()
            {
               var f = [Throws(typeof(ArgumentNullException), typeof(ArgumentNullException))] bool (string str) => { return true; };
            }
        }
        """;

        var expected = Verifier.DuplicateExceptionDeclared("ArgumentNullException")
           .WithSpan(9, 17, 9, 85);

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }
}
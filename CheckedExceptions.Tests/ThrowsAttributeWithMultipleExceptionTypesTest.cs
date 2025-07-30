using Microsoft.CodeAnalysis.Testing;

namespace Sundstrom.CheckedExceptions.Tests;

using Verifier = CSharpAnalyzerVerifier<CheckedExceptionsAnalyzer, DefaultVerifier>;

public partial class ThrowsAttributeWithMultipleExceptionTypesTest
{
    [Fact]
    public async Task Should_NotReportDiagnostic()
    {
        var test = /* lang=c#-test */ """
        #nullable enable
        using System;
        using System.Linq;
        using System.Collections.Generic;

        public class Test
        {
            [Throws(typeof(InvalidOperationException), typeof(ArgumentException))]
            public void MethodThatThrows()
            {
                IEnumerable<string>? items = null;

                var x = items.First();

                throw new ArgumentException();
            }
        }
        """;

        await Verifier.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// The original behavior should be intact, as proven by other tests.
    /// </summary>
    [Fact]
    public async Task Should_NotReportDiagnostic_Control()
    {
        var test = /* lang=c#-test */ """
        #nullable enable
        using System;
        using System.Linq;
        using System.Collections.Generic;

        public class Test
        {
            [Throws(typeof(InvalidOperationException))] 
            [Throws(typeof(ArgumentException))]
            public void MethodThatThrows()
            {
                IEnumerable<string>? items = null;

                var x = items.First();

                throw new ArgumentException();
            }
        }
        """;

        await Verifier.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task DuplicateExceptionsTypes_InSeparateThrowAttributeDeclarations_ShouldReportDiagnostics()
    {
        var test = /* lang=c#-test */ """
        #nullable enable
        using System;
        using System.Linq;
        using System.Collections.Generic;

        public class Test
        {
            [Throws(typeof(InvalidOperationException))]
            [Throws(typeof(InvalidOperationException))]
            public void MethodThatThrows5()
            {

            }
        }
        """;

        var expected = Verifier.DuplicateExceptionDeclared("InvalidOperationException")
            .WithSpan(9, 20, 9, 45);

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task DuplicateExceptionsTypes_InOneThrowAttributeDeclaration_ShouldReportDiagnostics1()
    {
        var test = /* lang=c#-test */ """
        #nullable enable
        using System;
        using System.Linq;
        using System.Collections.Generic;

        public class Test
        {
            [Throws(
                typeof(InvalidOperationException),
                typeof(InvalidOperationException))]
            public void MethodThatThrows5()
            {

            }
        }
        """;

        var expected = Verifier.DuplicateExceptionDeclared("InvalidOperationException")
           .WithSpan(10, 16, 10, 41);

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task DuplicateExceptionsTypes_InOneAndASeparateThrowAttributeDeclaration_ShouldReportDiagnostics()
    {
        var test = /* lang=c#-test */ """
        #nullable enable
        using System;
        using System.Linq;
        using System.Collections.Generic;

        public class Test
        {
            [Throws(
                typeof(InvalidOperationException),
                typeof(InvalidOperationException))]
            [Throws(typeof(InvalidOperationException))]
            public void MethodThatThrows5()
            {

            }
        }
        """;

        var expected1 = Verifier.DuplicateExceptionDeclared("InvalidOperationException")
           .WithSpan(10, 16, 10, 41);

        var expected2 = Verifier.DuplicateExceptionDeclared("InvalidOperationException")
            .WithSpan(11, 20, 11, 45);

        await Verifier.VerifyAnalyzerAsync(test, expected1, expected2);
    }
}
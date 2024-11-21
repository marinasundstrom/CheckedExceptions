using Microsoft.CodeAnalysis.Testing;

namespace Sundstrom.CheckedExceptions.Tests;

using Verifier = CSharpAnalyzerVerifier<CheckedExceptionsAnalyzer, DefaultVerifier>;

public partial class ExtensionMethods
{
    [Fact]
    public async Task Foo()
    {
        var test = /* lang=c#-test */ """
        using System;
        using System.Collections.Generic;
        using System.Linq;

        public class ThrowTest
        {
            public void MethodThatThrows()
            {
                IEnumerable<string> items = [];

                var i = items.First();
            }
        }
        """;

        var expected1 = Verifier.Diagnostic("THROW001")
           .WithSpan(11, 17, 11, 30)
           .WithArguments("ArgumentNullException");

        var expected2 = Verifier.Diagnostic("THROW001")
           .WithSpan(11, 17, 11, 30)
           .WithArguments("InvalidOperationException");

        await Verifier.VerifyAnalyzerAsync(test, expected1, expected2);
    }

    [Fact]
    public async Task Foo2()
    {
        var test = /* lang=c#-test */ """
        #nullable enable
        using System;
        using System.Collections.Generic;
        using System.Linq;

        public class ThrowTest
        {
            public void MethodThatThrows()
            {
                IEnumerable<string> items = [];

                var i = items.First();
            }
        }
        """;

        var expected = Verifier.Diagnostic("THROW001")
           .WithSpan(12, 17, 12, 30)
           .WithArguments("InvalidOperationException");

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }
}
using Microsoft.CodeAnalysis.Testing;

namespace Sundstrom.CheckedExceptions.Tests;

using Verifier = CSharpAnalyzerVerifier<CheckedExceptionsAnalyzer, DefaultVerifier>;

public partial class NullableContextTest
{
    [Fact(DisplayName = "Nullable Context disabled, should report ArgumentNullException")]
    public async Task NullableContextDisabled_ShouldReportArgumentNullException()
    {
        var test = /* lang=c#-test */ """
            using System;
            using System.Collections.Generic;

            public class TestClass
            {
                public void TestMethod()
                {
                    TestMethod2("Test");
                }

                [Throws(typeof(ArgumentNullException))]
                public void TestMethod2(string f)
                {
                    
                }
            }
            """;


        var expected = Verifier.UnhandledException("ArgumentNullException")
            .WithSpan(8, 9, 8, 28);

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }

    [Fact(DisplayName = "Nullable Context enabled, should not report ArgumentNullException")]
    public async Task NullableContextEnabled_ShouldNotReportArgumentNullException()
    {
        var test = /* lang=c#-test */ """
            #nullable enable
            using System;
            using System.Collections.Generic;

            public class TestClass
            {
                public void TestMethod()
                {
                    TestMethod2("Test");
                }

                [Throws(typeof(ArgumentNullException))]
                public void TestMethod2(string f)
                {
                    
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(test);
    }
}
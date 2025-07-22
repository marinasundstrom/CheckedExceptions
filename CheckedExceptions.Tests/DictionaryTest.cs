using Microsoft.CodeAnalysis.Testing;

namespace Sundstrom.CheckedExceptions.Tests;

using Verifier = CSharpAnalyzerVerifier<CheckedExceptionsAnalyzer, DefaultVerifier>;

public partial class DictionaryTest
{

    /// <summary>
    /// Add is adding a new value by key, not replacing it.
    /// </summary>
    [Fact(DisplayName = "Adding key-value to Dictionary using indexer setter should report ArgumentExceptionException")]
    public async Task AddingKeyValueToDictionaryUsingAddMethod_ShouldReportArgumentExceptionException()
    {
        var test = /* lang=c#-test */ """
            #nullable enable
            using System;
            using System.Collections.Generic;

            public class TestClass
            {
                public void TestMethod2()
                {
                    Dictionary<string, int> test = new Dictionary<string, int>();
                    test.Add("Foo", 42);
                }
            }
            """;

        var expected = Verifier.UnhandledException("ArgumentException")
            .WithSpan(10, 14, 10, 28);

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }

    /// <summary>
    /// This is either adding or replacing a value.
    /// </summary>
    [Fact(DisplayName = "Adding key-value to Dictionary using indexer setter should report no exception")]
    public async Task AddingValueToDictionaryUsingIndexerSetter_ShouldReportNoException()
    {
        var test = /* lang=c#-test */ """
            #nullable enable
            using System;
            using System.Collections.Generic;

            public class TestClass
            {
                public void TestMethod2()
                {
                    Dictionary<string, int> test = new Dictionary<string, int>();
                    test["Foo"] = 42;
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(test);
    }

    [Fact(DisplayName = "Getting value from Dictionary using indexer getter should report KeyNotFoundException")]
    public async Task GettingValueFromDictionaryUsingIndexerGetter_ShouldReportKeyNotFoundException()
    {
        var test = /* lang=c#-test */ """
            #nullable enable
            using System;
            using System.Collections.Generic;

            public class TestClass
            {
                public void TestMethod2()
                {
                    Dictionary<string, int> test = new Dictionary<string, int>();
                    var x = test["Foo"];
                }
            }
            """;

        var expected = Verifier.UnhandledException("KeyNotFoundException")
            .WithSpan(10, 17, 10, 28);

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }
}
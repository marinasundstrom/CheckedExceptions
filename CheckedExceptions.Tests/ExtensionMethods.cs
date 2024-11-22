using Microsoft.CodeAnalysis.Testing;

namespace Sundstrom.CheckedExceptions.Tests;

using Verifier = CSharpAnalyzerVerifier<CheckedExceptionsAnalyzer, DefaultVerifier>;

public partial class ExtensionMethods
{
    // Non-nullable context: Warns about ArgumentNullException and InvalidOperationException for the source parameter.
    [Fact]
    public async Task WarnsForNonNullableContextWithMultipleExceptions()
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

        var expected1 = Verifier.MightBeThrown("ArgumentNullException")
           .WithSpan(11, 17, 11, 30);

        var expected2 = Verifier.MightBeThrown("InvalidOperationException")
           .WithSpan(11, 17, 11, 30);

        await Verifier.VerifyAnalyzerAsync(test, expected1, expected2);
    }

    // Nullable context: Doesn't warn about ArgumentNullException for the source parameter but warns about InvalidOperationException.
    [Fact]
    public async Task WarnsForNullableContextWithInvalidOperationException()
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

        var expected = Verifier.MightBeThrown("InvalidOperationException")
           .WithSpan(12, 17, 12, 30);

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }

    // Nullable context: Doesn't warn about ArgumentNullException; flow analysis ensures "items" is not null.
    [Fact]
    public async Task WarnsForNullableAssignedCollectionWithInvalidOperationException()
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
                IEnumerable<string>? items = [];

                var i = items.First();
            }
        }
        """;

        var expected = Verifier.MightBeThrown("InvalidOperationException")
           .WithSpan(12, 17, 12, 30);

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }

    // Nullable context: Warns about accessing a nullable "items" which can be null; flow analysis detects potential null access.
    [Fact]
    public async Task WarnsForNullableUnassignedCollectionWithInvalidOperationException()
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
                IEnumerable<string>? items = null;

                var i = items.First();
            }
        }
        """;

        var expected = Verifier.MightBeThrown("InvalidOperationException")
           .WithSpan(12, 17, 12, 30);

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }
}
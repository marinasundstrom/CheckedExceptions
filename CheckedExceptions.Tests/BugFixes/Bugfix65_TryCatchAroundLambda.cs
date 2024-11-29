using Microsoft.CodeAnalysis.Testing;

namespace Sundstrom.CheckedExceptions.Tests.BugFixes;

using Verifier = CSharpAnalyzerVerifier<CheckedExceptionsAnalyzer, DefaultVerifier>;

public partial class Bugfix65_TryCatchAroundLambda
{
    [Fact]
    public async Task LambdaExpression_ThrowsException_ReportsDiagnostic()
    {
        var test = /* lang=c#-test */ """
        using System;

        public class Test
        {
            public void Foo()
            {
                var x = bool (string s) => {
                    throw new InvalidOperationException();
                    return true;
                };
            }
        }
        """;

        var expected = Verifier.IsThrown("InvalidOperationException")
            .WithSpan(8, 13, 8, 51);

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task LambdaInTryCatch_CatchBlockShouldNotSuppressDiagnostic()
    {
        var test = /* lang=c#-test */ """
        using System;

        public class Test
        {
            public void Foo()
            {
                try
                {
                    var x = bool (string s) => {
                        throw new InvalidOperationException();
                        return true;
                    };
                }
                catch (InvalidOperationException e)
                {
                    
                }
            }
        }
        """;

        var expected = Verifier.IsThrown("InvalidOperationException")
            .WithSpan(10, 17, 10, 55);

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task LocalFunction_ThrowsException_ReportsDiagnostic()
    {
        var test = /* lang=c#-test */ """
        using System;

        public class Test
        {
            public void Foo()
            {
                bool Test (string s)
                {
                    throw new InvalidOperationException();
                    return true;
                }
            }
        }
        """;

        var expected = Verifier.IsThrown("InvalidOperationException")
            .WithSpan(9, 13, 9, 51);

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task LocalFunctionInTryCatch_CatchBlockShouldNotSuppressDiagnostic()
    {
        var test = /* lang=c#-test */ """
        using System;

        public class Test
        {
            public void Foo()
            {
                try
                {
                    bool Test (string s)
                    {
                        throw new InvalidOperationException();
                        return true;
                    }
                }
                catch (InvalidOperationException e)
                {
                    
                }
            }
        }
        """;

        var expected = Verifier.IsThrown("InvalidOperationException")
            .WithSpan(11, 17, 11, 55);

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }
}
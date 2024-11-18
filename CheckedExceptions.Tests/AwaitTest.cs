using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

namespace CheckedExceptions.Tests;

using Verifier = CSharpAnalyzerVerifier<CheckedExceptionsAnalyzer, DefaultVerifier>;

public partial class AwaitTest
{
    [Fact]
    public async Task Should_ReportDiagnostic_When_AwaitingAsyncMethod_WithoutCatch()
    {
        var test = /* lang=c#-test */ """
            using System;
            using System.Threading.Tasks;

            public class Test
            {
                [Throws(typeof(InvalidOperationException))]
                public Task<int> GetValueAsync() 
                {
                    return Task.FromResult(2);
                }

                public async Task Run()
                {
                    await GetValueAsync();
                }
            }
            """;

        var expected = Verifier.Diagnostic("THROW001")
            .WithSpan(14, 15, 14, 30)
            .WithArguments("InvalidOperationException");

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task Should_NotReportDiagnostic_When_AwaitingAsyncMethod_WithCatch()
    {
        var test = /* lang=c#-test */ """
            using System;
            using System.Threading.Tasks;

            public class Test
            {
                [Throws(typeof(InvalidOperationException))]
                public Task<int> GetValueAsync() 
                {
                    return Task.FromResult(2);
                }

                public async Task Run()
                {
                    try
                    {
                        await GetValueAsync();
                    }
                    catch(InvalidOperationException) 
                    {
                    
                    }
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task Should_ReportDiagnostic_When_AwaitingAsyncMethod_ViaThis_WithoutCatch()
    {
        var test = /* lang=c#-test */ """
            using System;
            using System.Threading.Tasks;

            public class Test
            {
                [Throws(typeof(InvalidOperationException))]
                public Task<int> GetValueAsync() 
                {
                    return Task.FromResult(2);
                }

                public async Task Run()
                {
                    await this.GetValueAsync();
                }
            }
            """;

        var expected = Verifier.Diagnostic("THROW001")
            .WithSpan(14, 15, 14, 35)
            .WithArguments("InvalidOperationException");

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }
}
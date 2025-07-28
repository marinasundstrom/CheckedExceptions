using System.ComponentModel;

using Microsoft.CodeAnalysis.Testing;

namespace Sundstrom.CheckedExceptions.Tests.BugFixes;

using Verifier = CSharpAnalyzerVerifier<CheckedExceptionsAnalyzer, DefaultVerifier>;

public partial class Bugfix50_LambdaScopeBug
{
    [Fact]
    public async Task Should_ReportDiagnostics_ForUnhandledThrowUnauthorizedAccessExceptionInLambda()
    {
        var test = /* lang=c#-test */ """
        using System;

        public class Test
        {
            public void ProcessBatchOrders()
            {
                var batchProcessor = [Throws(typeof(InvalidOperationException))]() =>
                {
                    throw new UnauthorizedAccessException("User is not authorized to fetch orders.");
                };
            }
        }
        """;

        var expected = Verifier.UnhandledException("UnauthorizedAccessException")
            .WithSpan(9, 13, 9, 94);

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task Should_NotReportDiagnostics_ForHandledThrowUnauthorizedAccessExceptionInLambda()
    {
        var test = /* lang=c#-test */ """
        using System;

        public class Test
        {
            public void ProcessBatchOrders()
            {
                var batchProcessor = [Throws(typeof(InvalidOperationException)), Throws(typeof(UnauthorizedAccessException))]() =>
                {
                    throw new UnauthorizedAccessException("User is not authorized to fetch orders.");
                };
            }
        }
        """;

        await Verifier.VerifyAnalyzerAsync(test);
    }

    /// <summary>
    /// Verifies the bug fix
    /// </summary>
    [Fact]
    public async Task Should_ReportDiagnostics_ForUnhandledThrowUnauthorizedAccessExceptionInLambda_ThrowsUnauthorizedAccessExceptionOnMethod_ShouldNotAffect()
    {
        var test = /* lang=c#-test */ """
        using System;

        public class Test
        {
            [Throws(typeof(UnauthorizedAccessException))]
            public void ProcessBatchOrders()
            {
                var batchProcessor = [Throws(typeof(InvalidOperationException))]() =>
                {
                    throw new UnauthorizedAccessException("User is not authorized to fetch orders.");
                };
            }
        }
        """;

        var expected = Verifier.UnhandledException("UnauthorizedAccessException")
            .WithSpan(10, 13, 10, 94);

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }
}
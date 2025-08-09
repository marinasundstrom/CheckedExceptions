namespace Sundstrom.CheckedExceptions.Tests.CodeFixes.BugFixes;

using System.Threading.Tasks;

using Xunit;

using Verifier = CSharpCodeFixVerifier<CheckedExceptionsAnalyzer, AddCatchClauseToTryCodeFixProvider, Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

public class BugFix120_FixWronglyOfferedInLambdaInsideTry
{
    [Fact]
    public async Task FixShouldNotBeOffered_WhenThrowInsideLambdaBody_DeclaredInTryBlock()
    {
        var testCode = /*lang=c#-test*/ """
using System;

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod()
        {
            try
            {
                Action action = () =>
                {
                    throw new InvalidOperationException();
                };
                action();
            }
            catch {}
        }
    }
}
""";

        var expected = /*lang=c#-test*/ """
using System;

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod()
        {
            try
            {
                Action action = () =>
                {
                    throw new InvalidOperationException();
                };
                action();
            }
            catch {}
        }
    }
}
""";

        var expectedDiagnostic = Verifier.UnhandledException("InvalidOperationException")
             .WithSpan(13, 21, 13, 59);

        // Expect fix to be registered and applied
        await Verifier.VerifyCodeFixAsync(testCode, [expectedDiagnostic], expected, expectedIncrementalIterations: 0, setup: options =>
        {
            options.DisabledDiagnostics.Add(CheckedExceptionsAnalyzer.DiagnosticIdRedundantCatchAllClause);
        });
    }

    [Fact]
    public async Task FixShouldBeOffered_WhenThrowInsideTryBlock_InLambdaBody()
    {
        var testCode = /*lang=c#-test*/ """
using System;

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod()
        {
            try
            {
                Action action = () =>
                {
                    try
                    {
                        throw new ArgumentException();

                        throw new InvalidOperationException();
                    }
                    catch (ArgumentException argumentException)
                    {
                    }
                };
                action();
            }
            catch {}
        }
    }
}
""";

        var expected = /*lang=c#-test*/ """
using System;

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod()
        {
            try
            {
                Action action = () =>
                {
                    try
                    {
                        throw new ArgumentException();

                        throw new InvalidOperationException();
                    }
                    catch (ArgumentException argumentException)
                    {
                    }
                    catch (InvalidOperationException invalidOperationException)
                    {
                    }
                };
                action();
            }
            catch {}
        }
    }
}
""";

        var expectedDiagnostic = Verifier.UnhandledException("InvalidOperationException")
            .WithSpan(17, 25, 17, 63);

        // Expect no fix to be registered
        await Verifier.VerifyCodeFixAsync(testCode, [expectedDiagnostic], expected, setup: options =>
        {
            options.DisabledDiagnostics.Add(CheckedExceptionsAnalyzer.DiagnosticIdRedundantCatchAllClause);
        });
    }
}
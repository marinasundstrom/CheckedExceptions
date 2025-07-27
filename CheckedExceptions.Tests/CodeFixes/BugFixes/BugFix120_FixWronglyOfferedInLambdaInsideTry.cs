namespace Sundstrom.CheckedExceptions.Tests.CodeFixes.BugFixes;

using System.Threading.Tasks;

using Xunit;

using Verifier = CSharpCodeFixVerifier<CheckedExceptionsAnalyzer, AddCatchClauseToTryCodeFixProvider, Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

public class BugFix120_FixWronglyOfferedInLambdaInsideTry
{
    [Fact]
    public async Task FixNotOffered_WhenThrowInsideLambdaBody_DeclaredInTryBlock()
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

        // Expect no fix to be registered
        await Verifier.VerifyCodeFixAsync(testCode, [], expected, expectedIncrementalIterations: 0);
    }

    [Fact]
    public async Task FixOffered_WhenThrowInsideTryBlock_InLambdaBody()
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
        await Verifier.VerifyCodeFixAsync(testCode, [expectedDiagnostic], expected);
    }
}
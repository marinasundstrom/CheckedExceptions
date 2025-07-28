namespace Sundstrom.CheckedExceptions.Tests.CodeFixes.BugFixes;

using System.Threading.Tasks;

using Xunit;

using Verifier = CSharpCodeFixVerifier<CheckedExceptionsAnalyzer, AddThrowsAttributeCodeFixProvider, Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

public class BugFix139
{
    [Fact]
    public async Task FixShouldNotBeOffered_WhenThrowInsideLambdaBody_DeclaredInTryBlock()
    {
        var testCode = /*lang=c#-test*/ """
using System;
using System.Linq;

public class TestClass
{
    public int TestProp => Test(42);

    [Throws(typeof(InvalidOperationException))]
    public int Test(int x)
    {
        return 0;
    }
}
""";

        var expected = /*lang=c#-test*/ """
using System;
using System.Linq;

public class TestClass
{
    [Throws(typeof(InvalidOperationException))]
    public int TestProp => Test(42);

    [Throws(typeof(InvalidOperationException))]
    public int Test(int x)
    {
        return 0;
    }
}
""";

        var expectedDiagnostic = Verifier.UnhandledException("InvalidOperationException")
             .WithSpan(6, 28, 6, 36);

        // Expect fix to be registered and applied
        await Verifier.VerifyCodeFixAsync(testCode, [expectedDiagnostic], expected);
    }
}
namespace Sundstrom.CheckedExceptions.Tests.CodeFixes.BugFixes;

using System.Threading.Tasks;

using Xunit;

using Verifier = CSharpCodeFixVerifier<CheckedExceptionsAnalyzer, AddThrowsAttributeCodeFixProvider, Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

public class BugFix118_UnexpectedHandlingOfLeadingTrivia
{
    [Fact]
    public async Task AddsThrowsAttribute()
    {
        var testCode = /* lang=c#-test */  """
using System;

namespace TestNamespace
{
    public class TestClass
    {
        // Test
        public void TestMethod()
        {
            // Should trigger THROW001
            throw new Exception();
        }
    }
}
""";

        var fixedCode = /* lang=c#-test */  """
using System;

namespace TestNamespace
{
    public class TestClass
    {
        // Test
        [Throws(typeof(Exception))]
        public void TestMethod()
        {
            // Should trigger THROW001
            throw new Exception();
        }
    }
}
""";

        var expectedDiagnostic = Verifier.UnhandledException("Exception")
            .WithSpan(11, 13, 11, 35);

        await Verifier.VerifyCodeFixAsync(testCode, expectedDiagnostic, fixedCode);
    }
}
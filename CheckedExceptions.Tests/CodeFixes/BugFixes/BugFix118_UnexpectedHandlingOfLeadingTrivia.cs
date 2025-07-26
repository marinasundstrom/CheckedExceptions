namespace Sundstrom.CheckedExceptions.Tests.CodeFixes.BugFixes;

using System.Threading.Tasks;

using Xunit;

using Verifier = CSharpCodeFixVerifier<CheckedExceptionsAnalyzer, AddThrowsAttributeCodeFixProvider, Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

public class BugFix118_UnexpectedHandlingOfLeadingTrivia
{
    [Fact]
    public async Task AddsThrowsAttribute_ToMethod_AfterLeadingTriviaRemoved()
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

    [Fact]
    public async Task AddsThrowsAttribute_ToPropertyAccessor_AfterLeadingTriviaRemoved()
    {
        var testCode = /* lang=c#-test */  """
using System;

namespace TestNamespace
{
    public class TestClass
    {
        public int TestProp
        {
            // Test
            get
            {
                // Should trigger THROW001
                throw new Exception();
            }
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
        public int TestProp
        {
            // Test
            [Throws(typeof(Exception))]
            get
            {
                // Should trigger THROW001
                throw new Exception();
            }
        }
    }
}
""";

        var expectedDiagnostic = Verifier.UnhandledException("Exception")
            .WithSpan(13, 17, 13, 39);

        await Verifier.VerifyCodeFixAsync(testCode, expectedDiagnostic, fixedCode);
    }
}
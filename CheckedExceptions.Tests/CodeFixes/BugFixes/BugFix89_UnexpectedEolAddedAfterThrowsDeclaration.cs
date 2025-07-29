namespace Sundstrom.CheckedExceptions.Tests.CodeFixes.BugFixes;

using System.Threading.Tasks;

using Xunit;

using Verifier = CSharpCodeFixVerifier<CheckedExceptionsAnalyzer, AddThrowsDeclarationCodeFixProvider, Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

public class BugFix89_UnexpectedEolAddedAfterThrowsDeclaration
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
    public async Task AddsThrowsAttribute_WithPrecedingConstruct()
    {
        var testCode = /* lang=c#-test */  """
using System;

namespace TestNamespace
{
    public class TestClass
    {
        int x = 2;

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
        int x = 2;

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
            .WithSpan(12, 13, 12, 35);

        await Verifier.VerifyCodeFixAsync(testCode, expectedDiagnostic, fixedCode);
    }
}
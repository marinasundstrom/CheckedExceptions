namespace Sundstrom.CheckedExceptions.Tests.CodeFixes;

using System.Threading.Tasks;

using Xunit;
using Xunit.Abstractions;

using Verifier = CSharpCodeFixVerifier<CheckedExceptionsAnalyzer, AddCatchClauseToTryCodeFixProvider, Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

public class AddCatchClauseToTryCodeFixProviderTests
{
    [Fact]
    public async Task TryExists_Should_AddCatchClause()
    {
        var testCode = /* lang=c#-test */  """
using System;

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod()
        {
            try
            {
                throw new InvalidOperationException();

                throw new ArgumentException();
            }
            catch (InvalidOperationException ex)
            {
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
        public void TestMethod()
        {
            try
            {
                throw new InvalidOperationException();

                throw new ArgumentException();
            }
            catch (InvalidOperationException ex)
            {
            }
            catch (ArgumentException ex2)
            {
            }
        }
    }
}
""";

        var expectedDiagnostic = Verifier.UnhandledException("ArgumentException")
            .WithSpan(13, 17, 13, 47);

        await Verifier.VerifyCodeFixAsync(testCode, expectedDiagnostic, fixedCode);
    }

    [Fact]
    public async Task NoTryExists_ShouldNot_AddCatchClause()
    {
        var testCode = /* lang=c#-test */  """
using System;

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod()
        {
            throw new InvalidOperationException();
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
        public void TestMethod()
        {
            throw new InvalidOperationException();
        }
    }
}
""";

        var expectedDiagnostic = Verifier.UnhandledException("InvalidOperationException")
            .WithSpan(9, 13, 9, 51);

        await Verifier.VerifyCodeFixAsync(testCode, expectedDiagnostic, fixedCode);
    }
}
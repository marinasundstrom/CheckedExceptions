namespace Sundstrom.CheckedExceptions.Tests.CodeFixes;

using System.Threading.Tasks;

using Microsoft.CodeAnalysis.Testing;
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
            catch (ArgumentException argumentException)
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

        await Verifier.VerifyCodeFixAsync(testCode, expectedDiagnostic, fixedCode, expectedIncrementalIterations: 0);
    }

    [Fact]
    public async Task FixAll_AddsCatchClauses_ForMultipleUnhandledExceptions()
    {
        var testCode = /* lang=c#-test */  """
using System;

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod1()
        {
            try
            {
                throw new InvalidOperationException();
            }
            catch (ArgumentException ex)
            {
            }
        }

        public void TestMethod2()
        {
            try
            {
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
        public void TestMethod1()
        {
            try
            {
                throw new InvalidOperationException();
            }
            catch (ArgumentException ex)
            {
            }
            catch (InvalidOperationException invalidOperationException)
            {
            }
        }

        public void TestMethod2()
        {
            try
            {
                throw new ArgumentException();
            }
            catch (InvalidOperationException ex)
            {
            }
            catch (ArgumentException argumentException)
            {
            }
        }
    }
}
""";

        var expectedDiagnostic1 = Verifier.UnhandledException("InvalidOperationException")
            .WithSpan(11, 17, 11, 55);
        var expectedDiagnostic2 = Verifier.UnhandledException("ArgumentException")
            .WithSpan(22, 17, 22, 47);

        await Verifier.VerifyCodeFixAsync(
            testCode,
            [expectedDiagnostic1, expectedDiagnostic2],
            fixedCode,
            expectedIncrementalIterations: 2,
            setup: test => test.CodeFixTestBehaviors &= ~CodeFixTestBehaviors.SkipFixAllCheck);
    }
}
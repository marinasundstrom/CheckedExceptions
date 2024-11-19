namespace Sundstrom.CheckedExceptions.Tests.CodeFixes;

using System.Threading.Tasks;
using Xunit;

using Verifier = CSharpCodeFixVerifier<CheckedExceptionsAnalyzer, AddTryCatchBlockCodeFixProvider, Microsoft.CodeAnalysis.Testing.DefaultVerifier>;
using Xunit.Abstractions;

public class AddTryCatchBlockCodeFixProviderTests
{
    [Fact]
    public async Task AddTryCatch_ToMethod_WhenUnhandledExceptionThrown()
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
        public void TestMethod()
        {
            try
            {
                // Should trigger THROW001
                throw new Exception();
            }
            catch (Exception ex)
            {
            }
        }
    }
}
""";

        var expectedDiagnostic = Verifier.Diagnostic("THROW001")
            .WithSpan(10, 13, 10, 35)
            .WithArguments("Exception");

        await Verifier.VerifyCodeFixAsync(testCode, expectedDiagnostic, fixedCode);
    }

    [Fact]
    public async Task AddTryCatch_ToMethod_WhenUnhandledException()
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
            DoSomething();
        }

        [Throws(typeof(InvalidOperationException))]
        public void DoSomething()
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
            try
            {
                // Should trigger THROW001
                DoSomething();
            }
            catch (InvalidOperationException ex)
            {
            }
        }

        [Throws(typeof(InvalidOperationException))]
        public void DoSomething()
        {
            throw new InvalidOperationException();
        }
    }
}
""";

        var expectedDiagnostic = Verifier.Diagnostic("THROW001")
            .WithSpan(10, 13, 10, 26)
            .WithArguments("InvalidOperationException");

        await Verifier.VerifyCodeFixAsync(testCode, expectedDiagnostic, fixedCode);
    }
}
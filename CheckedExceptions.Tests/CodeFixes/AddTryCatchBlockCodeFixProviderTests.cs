namespace Sundstrom.CheckedExceptions.Tests.CodeFixes;

using System.Threading.Tasks;

using Xunit;
using Xunit.Abstractions;

using Verifier = CSharpCodeFixVerifier<CheckedExceptionsAnalyzer, AddTryCatchBlockCodeFixProvider, Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

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

        var expectedDiagnostic = Verifier.IsThrown("Exception")
            .WithSpan(10, 13, 10, 35);

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

        var expectedDiagnostic = Verifier.MightBeThrown("InvalidOperationException")
            .WithSpan(10, 13, 10, 26);

        await Verifier.VerifyCodeFixAsync(testCode, expectedDiagnostic, fixedCode);
    }

    [Fact]
    public async Task AddTryCatch_ToMethod_WhenUnhandledException1()
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
            var x = DoSomething();
            x = x + 1;
        }

        [Throws(typeof(InvalidOperationException))]
        public int DoSomething()
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
                var x = DoSomething();
                x = x + 1;
            }
            catch (InvalidOperationException ex)
            {
            }
        }

        [Throws(typeof(InvalidOperationException))]
        public int DoSomething()
        {
            throw new InvalidOperationException();
        }
    }
}
""";

        var expectedDiagnostic = Verifier.MightBeThrown("InvalidOperationException")
            .WithSpan(10, 21, 10, 34);

        await Verifier.VerifyCodeFixAsync(testCode, expectedDiagnostic, fixedCode, 1);
    }

    [Fact]
    public async Task AddTryCatch_ToMethod_WhenUnhandledException3()
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
                DoSomething();
            }
            catch (InvalidOperationException ex)
            {
                // Should trigger THROW001
                DoSomething();
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
                DoSomething();
            }
            catch (InvalidOperationException ex)
            {
                try
                {
                    // Should trigger THROW001
                    DoSomething();
                }
                catch (InvalidOperationException ex2)
                {
                }
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

        var expectedDiagnostic1 = Verifier.MightBeThrown("InvalidOperationException")
            .WithSpan(16, 17, 16, 30);

        await Verifier.VerifyCodeFixAsync(testCode, [expectedDiagnostic1], fixedCode, 1);
    }
}
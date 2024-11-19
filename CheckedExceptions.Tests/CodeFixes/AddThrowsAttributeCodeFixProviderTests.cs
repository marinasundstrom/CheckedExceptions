namespace Sundstrom.CheckedExceptions.Tests.CodeFixes;

using System.Threading.Tasks;
using Xunit;

using Verifier = CSharpCodeFixVerifier<CheckedExceptionsAnalyzer, AddThrowsAttributeCodeFixProvider, Microsoft.CodeAnalysis.Testing.DefaultVerifier>;
using Xunit.Abstractions;

public class AddThrowsAttributeCodeFixProviderTests
{
    [Fact]
    public async Task AddsThrowsAttribute_ToMethod_WhenUnhandledExceptionThrown()
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

        var expectedDiagnostic = Verifier.Diagnostic("THROW001")
            .WithSpan(10, 13, 10, 35)
            .WithArguments("Exception");

        await Verifier.VerifyCodeFixAsync(testCode, expectedDiagnostic, fixedCode);
    }

    [Fact]
    public async Task AddsThrowsAttribute_ToConstructor_WhenUnhandledExceptionThrown()
    {
        var testCode = /* lang=c#-test */  """
using System;

namespace TestNamespace
{
    public class TestClass
    {
        public TestClass()
        {
            // Should trigger THROW001
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
        [Throws(typeof(InvalidOperationException))]
        public TestClass()
        {
            // Should trigger THROW001
            throw new InvalidOperationException();
        }
    }
}
""";

        var expectedDiagnostic = Verifier.Diagnostic("THROW001")
            .WithSpan(10, 13, 10, 51)
            .WithArguments("InvalidOperationException");

        await Verifier.VerifyCodeFixAsync(testCode, expectedDiagnostic, fixedCode);
    }

    [Fact]
    public async Task DoesNotAddThrowsAttribute_IfAlreadyDeclared()
    {
        var testCode = /* lang=c#-test */  """
using System;

namespace TestNamespace
{
    public class TestClass
    {
        [Throws(typeof(InvalidOperationException))]
        public void TestMethod()
        {
            // Should trigger THROW001 but attribute already present
            throw new InvalidOperationException();
        }
    }
}
""";

        // Since the ThrowsAttribute is already present, the analyzer should not report THROW001
        await Verifier.VerifyCodeFixAsync(testCode);
    }

    [Fact]
    public async Task AddsThrowsAttribute_ToLambda_WhenUnhandledExceptionThrown()
    {
        var testCode = /* lang=c#-test */  """
using System;
using System.Linq;

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod()
        {
            Action action = () => 
            {
                // Should trigger THROW001
                throw new ArgumentNullException();
            };
            action();
        }
    }
}
""";

        var fixedCode = /* lang=c#-test */  """
using System;
using System.Linq;

namespace TestNamespace
{
    public class TestClass
    {
        [Throws(typeof(ArgumentNullException))]
        public void TestMethod()
        {
            Action action = [Throws(typeof(ArgumentNullException))]
            () =>
            {
                // Should trigger THROW001
                throw new ArgumentNullException();
            };
            action();
        }
    }
}
""";

        var expectedDiagnostic = Verifier.Diagnostic("THROW001")
            .WithSpan(13, 17, 13, 51)
            .WithArguments("ArgumentNullException");

        await Verifier.VerifyCodeFixAsync(testCode, expectedDiagnostic, fixedCode, expectedIncrementalIterations: 2);
    }

    [Fact]
    public async Task AddsThrowsAttribute_ToLocalFunction_WhenUnhandledExceptionThrown()
    {
        var testCode = /* lang=c#-test */  """
using System;

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod()
        {
            void LocalFunction()
            {
                // Should trigger THROW001
                throw new NotImplementedException();
            }
            LocalFunction();
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
        [Throws(typeof(NotImplementedException))]
        public void TestMethod()
        {
            [Throws(typeof(NotImplementedException))]
            void LocalFunction()
            {
                // Should trigger THROW001
                throw new NotImplementedException();
            }

            LocalFunction();
        }
    }
}
""";

        var expectedDiagnostic = Verifier.Diagnostic("THROW001")
            .WithSpan(12, 17, 12, 53)
            .WithArguments("NotImplementedException");

        await Verifier.VerifyCodeFixAsync(testCode, expectedDiagnostic, fixedCode, expectedIncrementalIterations: 2);
    }

    [Fact]
    public async Task AddsThrowsAttribute_ToAccessor_WhenUnhandledExceptionThrown()
    {
        var testCode = /* lang=c#-test */  """
using System;

namespace TestNamespace
{
    public class TestClass
    {
        private string _field;

        public string Property
        {
            get
            {
                // Should trigger THROW001
                throw new Exception();
            }
            set
            {
                _field = value;
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
        private string _field;

        public string Property
        {
            [Throws(typeof(Exception))]
            get
            {
                // Should trigger THROW001
                throw new Exception();
            }
            set
            {
                _field = value;
            }
        }
    }
}
""";

        var expectedDiagnostic = Verifier.Diagnostic("THROW001")
            .WithSpan(14, 17, 14, 39)
            .WithArguments("Exception");

        await Verifier.VerifyCodeFixAsync(testCode, expectedDiagnostic, fixedCode);
    }
}
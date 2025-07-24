namespace Sundstrom.CheckedExceptions.Tests.CodeFixes;

using System.Threading.Tasks;

using Xunit;
using Xunit.Abstractions;

using Verifier = CSharpCodeFixVerifier<CheckedExceptionsAnalyzer, AddThrowsAttributeCodeFixProvider, Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

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

        var expectedDiagnostic = Verifier.UnhandledException("Exception")
            .WithSpan(10, 13, 10, 35);

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

        var expectedDiagnostic = Verifier.UnhandledException("InvalidOperationException")
            .WithSpan(10, 13, 10, 51);

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
        await Verifier.VerifyCodeFixAsync(testCode, []);
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

        var expectedDiagnostic = Verifier.UnhandledException("ArgumentNullException")
            .WithSpan(13, 17, 13, 51);

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
                throw new InvalidOperationException();
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
        [Throws(typeof(InvalidOperationException))]
        public void TestMethod()
        {
            [Throws(typeof(InvalidOperationException))]
            void LocalFunction()
            {
                // Should trigger THROW001
                throw new InvalidOperationException();
            }

            LocalFunction();
        }
    }
}
""";

        var expectedDiagnostic = Verifier.UnhandledException("InvalidOperationException")
            .WithSpan(12, 17, 12, 55);

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

        var expectedDiagnostic = Verifier.UnhandledException("Exception")
            .WithSpan(14, 17, 14, 39);

        await Verifier.VerifyCodeFixAsync(testCode, expectedDiagnostic, fixedCode);
    }

    [Fact]
    public async Task AddsThrowsAttribute_ToAccessor_WhenUnhandledExceptionThrown2()
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
                throw new InvalidOperationException();

                // Should trigger THROW001
                throw new ArgumentNullException();
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
            [Throws(typeof(InvalidOperationException), typeof(ArgumentNullException))]
            get
            {
                // Should trigger THROW001
                throw new InvalidOperationException();

                // Should trigger THROW001
                throw new ArgumentNullException();
            }

            set
            {
                _field = value;
            }
        }
    }
}
""";

        var expectedDiagnostic1 = Verifier.UnhandledException("InvalidOperationException")
            .WithSpan(14, 17, 14, 55);

        var expectedDiagnostic2 = Verifier.UnhandledException("ArgumentNullException")
            .WithSpan(17, 17, 17, 51);

        await Verifier.VerifyCodeFixAsync(testCode, [expectedDiagnostic1, expectedDiagnostic2], fixedCode, 2);
    }
}
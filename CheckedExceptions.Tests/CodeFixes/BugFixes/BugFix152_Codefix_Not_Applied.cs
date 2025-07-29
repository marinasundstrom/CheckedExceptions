namespace Sundstrom.CheckedExceptions.Tests.CodeFixes.BugFixes;

using System.Threading.Tasks;

using Microsoft.CodeAnalysis.Testing;

using Xunit;

using Verifier = CSharpCodeFixVerifier<CheckedExceptionsAnalyzer, SurroundWithTryCatchCodeFixProvider, Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

public class BugFix152_Codefix_Not_Applied
{
    [Fact]
    public async Task AddTryCatch_ToTopLevel_WhenUnhandledExceptionThrown()
    {
        var testCode = /* lang=c#-test */  """
using System;
using System.Linq;

namespace TestNamespace
{
    public class TestClass
    {
        public void Foo() 
        {
            int TestMethod() => Test(41) + 1;
        }

        [Throws(typeof(InvalidOperationException))]
        public int Test(int x) 
        {
            return x;
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
        public void Foo() 
        {
            int TestMethod() { try { return Test(41) + 1; } catch (InvalidOperationException invalidOperationException) { } }
        }

        [Throws(typeof(InvalidOperationException))]
        public int Test(int x) 
        {
            return x;
        }
    }
}
""";
        var expectedDiagnostic = Verifier.UnhandledException("InvalidOperationException")
            .WithSpan(10, 33, 10, 41);

        await Verifier.VerifyCodeFixAsync(testCode, [expectedDiagnostic], fixedCode, setup: x => x.CompilerDiagnostics = CompilerDiagnostics.None);
    }

    [Fact]
    public async Task AddTryCatch_ToTopLevel_WhenUnhandledExceptionThrown2()
    {
        var testCode = /* lang=c#-test */  """
using System;
using System.Linq;

namespace TestNamespace
{
    public class TestClass
    {
        public void Foo() 
        {
            var f = int () => Test(41) + 1;
        }

        [Throws(typeof(InvalidOperationException))]
        public int Test(int x) 
        {
            return x;
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
        public void Foo() 
        {
            var f = int () => { try { return Test(41) + 1; } catch (InvalidOperationException invalidOperationException) { } };
        }

        [Throws(typeof(InvalidOperationException))]
        public int Test(int x) 
        {
            return x;
        }
    }
}
""";
        var expectedDiagnostic = Verifier.UnhandledException("InvalidOperationException")
            .WithSpan(10, 31, 10, 39);

        await Verifier.VerifyCodeFixAsync(testCode, [expectedDiagnostic], fixedCode, setup: x => x.CompilerDiagnostics = CompilerDiagnostics.None);
    }
}
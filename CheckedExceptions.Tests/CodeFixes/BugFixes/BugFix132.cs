namespace Sundstrom.CheckedExceptions.Tests.CodeFixes.BugFixes;

using System.Threading.Tasks;

using Xunit;

using Verifier = CSharpCodeFixVerifier<CheckedExceptionsAnalyzer, AddCatchClauseToTryCodeFixProvider, Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

public class BugFix132
{
    [Fact]
    public async Task FixShouldNotBeOffered_WhenThrowInsideLambdaBody_DeclaredInTryBlock()
    {
        var testCode = /*lang=c#-test*/ """
using System;
using System.Linq;

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod()
        {
            try
            {
                For(x => Test(x));
            }
            catch {}
        }

        [Throws(typeof(InvalidOperationException))]
        public bool Test(int x) 
        {
            return true;
        }

        public void For(Func<int, bool> f)
        {
            
        }
    }
}
""";

        var expected = /*lang=c#-test*/ """
using System;
using System.Linq;

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod()
        {
            try
            {
                For(x => Test(x));
            }
            catch {}
        }

        [Throws(typeof(InvalidOperationException))]
        public bool Test(int x) 
        {
            return true;
        }

        public void For(Func<int, bool> f)
        {
            
        }
    }
}
""";

        var expectedDiagnostic = Verifier.UnhandledException("InvalidOperationException")
             .WithSpan(12, 26, 12, 33);

        // Expect fix to be registered and applied
        await Verifier.VerifyCodeFixAsync(testCode, [expectedDiagnostic], expected, expectedIncrementalIterations: 0);
    }

    [Fact]
    public async Task FixShouldBeOffered_WhenThrowInsideTryBlock_InLambdaBody()
    {
        var testCode = /*lang=c#-test*/ """
using System;
using System.Linq;

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod()
        {
            try
            {
                For(x => {
                    return Test(x);
                });
            }
            catch {}
        }

        [Throws(typeof(InvalidOperationException))]
        public bool Test(int x) 
        {
            return true;
        }

        public void For(Func<int, bool> f)
        {
            
        }
    }
}
""";

        var expected = /*lang=c#-test*/ """
using System;
using System.Linq;

namespace TestNamespace
{
    public class TestClass
    {
        public void TestMethod()
        {
            try
            {
                For(x => {
                    return Test(x);
                });
            }
            catch {}
        }

        [Throws(typeof(InvalidOperationException))]
        public bool Test(int x) 
        {
            return true;
        }

        public void For(Func<int, bool> f)
        {
            
        }
    }
}
""";

        var expectedDiagnostic = Verifier.UnhandledException("InvalidOperationException")
            .WithSpan(13, 28, 13, 35);

        // Expect no fix to be registered
        await Verifier.VerifyCodeFixAsync(testCode, [expectedDiagnostic], expected, expectedIncrementalIterations: 0);
    }
}
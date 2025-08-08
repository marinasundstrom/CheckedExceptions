namespace Sundstrom.CheckedExceptions.Tests.CodeFixes;

using System.Threading.Tasks;

using Xunit;
using Xunit.Abstractions;

using Verifier = CSharpCodeFixVerifier<CheckedExceptionsAnalyzer, IntroduceCatchClauseForRethrownExceptionCodeFixProvider, Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

public class IntroduceCatchClauseForRethrownExceptionCodeFixProviderTests
{
    [Fact]
    public async Task CatchAll_FixApplied()
    {
        var testCode = /* lang=c#-test */  """
using System;

namespace TestNamespace
{
    public class TestClass
    {
        public void Foo2()
        {
            try
            {
                MethodThatThrows();
            }
            catch
            {
                throw;
            }
        }

        [Throws(typeof(InvalidOperationException))]
        public void MethodThatThrows()
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
        public void Foo2()
        {
            try
            {
                MethodThatThrows();
            }
            catch (InvalidOperationException invalidOperationException)
            {
            }
            catch
            {
                throw;
            }
        }

        [Throws(typeof(InvalidOperationException))]
        public void MethodThatThrows()
        {
            throw new InvalidOperationException();
        }
    }
}
""";

        var expectedDiagnostic = Verifier.UnhandledException("InvalidOperationException")
            .WithSpan(15, 17, 15, 23);

        await Verifier.VerifyCodeFixAsync(testCode, [expectedDiagnostic], fixedCode, setup: o =>
        {
            o.DisabledDiagnostics.Add(CheckedExceptionsAnalyzer.DiagnosticIdRedundantCatchAllClause);
        });
    }

    [Fact]
    public async Task TypedCatch_NoFixApplied()
    {
        var testCode = /* lang=c#-test */  """
using System;

namespace TestNamespace
{
    public class TestClass
    {
        public void Foo2()
        {
            try
            {
                MethodThatThrows();
            }
            catch (InvalidOperationException exc)
            {
                throw;
            }
        }

        [Throws(typeof(InvalidOperationException))]
        public void MethodThatThrows()
        {
            throw new InvalidOperationException();
        }
    }
}
""";

        var expectedDiagnostic = Verifier.UnhandledException("InvalidOperationException")
            .WithSpan(15, 17, 15, 23);

        await Verifier.VerifyCodeFixAsync(testCode, [expectedDiagnostic], testCode, expectedIncrementalIterations: 0, setup: o =>
        {
            o.DisabledDiagnostics.Add(CheckedExceptionsAnalyzer.DiagnosticIdRedundantCatchAllClause);
        });
    }
}
namespace Sundstrom.CheckedExceptions.Tests.CodeFixes;

using System.Threading.Tasks;

using Xunit;
using Xunit.Abstractions;

using Verifier = CSharpCodeFixVerifier<CheckedExceptionsAnalyzer, AddCatchClauseForRethrownExceptionCodeFixProvider, Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

public class AddCatchClauseForRethrownExceptionCodeFixProviderTests
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
}
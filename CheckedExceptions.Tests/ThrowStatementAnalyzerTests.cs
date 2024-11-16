using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace CheckedExceptions.Test;

using Verifier = CSharpCodeFixVerifier<CheckedExceptionsAnalyzer, AddThrowsAttributeCodeFixProvider, DefaultVerifier>;

public class ThrowStatementAnalyzerTests
{
    [Fact]
    public async Task MethodWithoutHandlingThrowsException_ShouldTriggerWarning()
    {
        // Basic case
        await Verifier.VerifyCodeFixAsync(
            source: """
public class Test
{
    public void Foo()
    {
        throw new NullReferenceException("Data source is null.");
    }
}
""",
            expected: [
                Verifier.Diagnostic().WithSpan(5, 9, 5, 66).WithArguments("NullReferenceException"),
                DiagnosticResult.CompilerError("CS0246").WithSpan(5, 19, 5, 41).WithArguments("NullReferenceException")],
            fixedSource: """
public class Test
{
    [Throws(typeof(NullReferenceException))]
    public void Foo()
    {
        throw new NullReferenceException("Data source is null.");
    }
}
""");
    }
}
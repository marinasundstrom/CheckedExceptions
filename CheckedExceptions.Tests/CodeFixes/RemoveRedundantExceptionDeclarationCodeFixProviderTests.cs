namespace Sundstrom.CheckedExceptions.Tests.CodeFixes;

using System.Threading.Tasks;

using Microsoft.CodeAnalysis.Testing;

using Xunit;
using Xunit.Abstractions;

using Verifier = CSharpCodeFixVerifier<CheckedExceptionsAnalyzer, RemoveRedundantExceptionDeclarationCodeFixProvider, Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

public class RemoveRedundantExceptionDeclarationCodeFixProviderTests
{
    [Fact]
    public async Task RemoveWholeAttribute()
    {
        var testCode = /* lang=c#-test */  """
#nullable enable
using System;

public class C
{
    [Throws(typeof(InvalidOperationException))]
    public void M()
    {
    }
}
""";

        var fixedCode = /* lang=c#-test */  """
#nullable enable
using System;

public class C
{
    public void M()
    {
    }
}
""";

        var expected = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdRedundantExceptionDeclaration)
            .WithArguments("InvalidOperationException")
            .WithSpan(6, 20, 6, 45);

        await Verifier.VerifyCodeFixAsync(testCode, [expected], fixedCode, setup: option =>
        {
            option.DisabledDiagnostics.Remove(CheckedExceptionsAnalyzer.DiagnosticIdRedundantExceptionDeclaration);
        });
    }

    [Fact]
    public async Task RemoveSingleArgumentFromAttribute()
    {
        var testCode = /* lang=c#-test */  """
#nullable enable
using System;

public class C
{
    [Throws(typeof(InvalidOperationException), typeof(ArgumentException))]
    public void M()
    {
        throw new ArgumentException();
    }
}
""";

        var fixedCode = /* lang=c#-test */  """
#nullable enable
using System;

public class C
{
    [Throws(typeof(ArgumentException))]
    public void M()
    {
        throw new ArgumentException();
    }
}
""";

        var expected = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdRedundantExceptionDeclaration)
            .WithArguments("InvalidOperationException")
            .WithSpan(6, 20, 6, 45);

        await Verifier.VerifyCodeFixAsync(testCode, [expected], fixedCode, setup: option =>
        {
            option.DisabledDiagnostics.Remove(CheckedExceptionsAnalyzer.DiagnosticIdRedundantExceptionDeclaration);
        });
    }
}

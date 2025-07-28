namespace Sundstrom.CheckedExceptions.Tests.CodeFixes;

using System.Threading.Tasks;

using Xunit;
using Xunit.Abstractions;

using Verifier = CSharpCodeFixVerifier<CheckedExceptionsAnalyzer, RemoveRedundantCatchClauseCodeFixProvider, Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

public class RemoveRedundantCatchClauseCodeFixProviderTests
{
    [Fact]
    public async Task RemoveCatchBlock()
    {
        var testCode = /* lang=c#-test */  """
#nullable enable
using System;

try
{
    int.Parse("a");
}
catch (FormatException formatException)
{
}
catch (OverflowException overflowException)
{
}
catch (ArgumentException argumentException)
{
}
""";

        var fixedCode = /* lang=c#-test */  """
#nullable enable
using System;

try
{
    int.Parse("a");
}
catch (FormatException formatException)
{
}
catch (OverflowException overflowException)
{
}
""";

        var expectedDiagnostic = Verifier.Diagnostic("THROW009")
                .WithArguments("System.ArgumentException")
                .WithSpan(14, 8, 14, 25);


        await Verifier.VerifyCodeFixAsync(testCode, expectedDiagnostic, fixedCode, executable: true);
    }
}
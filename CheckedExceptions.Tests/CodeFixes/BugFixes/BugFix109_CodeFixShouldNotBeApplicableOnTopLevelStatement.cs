namespace Sundstrom.CheckedExceptions.Tests.CodeFixes.BugFixes;

using System.Threading.Tasks;

using Xunit;

using Verifier = CSharpCodeFixVerifier<CheckedExceptionsAnalyzer, AddThrowsDeclarationCodeFixProvider, Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

public class BugFix109_CodeFixShouldNotBeApplicableOnTopLevelStatement
{
    [Fact]
    public async Task CodeFix_NotApplicable()
    {
        var testCode = /* lang=c#-test */  """
using System;

throw new Exception();
""";

        var fixedCode = /* lang=c#-test */  """
using System;

throw new Exception();
""";

        var expectedDiagnostic = Verifier.UnhandledException("Exception")
            .WithSpan(3, 1, 3, 23);

        await Verifier.VerifyCodeFixAsync(testCode, expectedDiagnostic, fixedCode, expectedIncrementalIterations: 0, executable: true);
    }
}
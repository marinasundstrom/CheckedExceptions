namespace Sundstrom.CheckedExceptions.Tests.CodeFixes.BugFixes;

using System.Threading.Tasks;

using Xunit;

using Verifier = CSharpCodeFixVerifier<CheckedExceptionsAnalyzer, AddTryCatchBlockCodeFixProvider, Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

public class BugFix94_TryCatchNotAppliedAtTopLevel
{
    [Fact]
    public async Task AddTryCatch_ToTopLevel_WhenUnhandledExceptionThrown()
    {
        var testCode = /* lang=c#-test */  """
using System;

throw new ArgumentException();
""";

        var fixedCode = /* lang=c#-test */  """
using System;

try
{
    throw new ArgumentException();
}
catch (ArgumentException argumentException)
{
}
""";

        var expectedDiagnostic = Verifier.UnhandledException("ArgumentException")
            .WithSpan(3, 1, 3, 31);

        await Verifier.VerifyCodeFixAsync(testCode, expectedDiagnostic, fixedCode);
    }
}
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

        var expectedDiagnostic = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdRedundantTypedCatchClause)
                .WithArguments("ArgumentException")
                .WithSpan(14, 8, 14, 25);

        await Verifier.VerifyCodeFixAsync(testCode, [expectedDiagnostic], fixedCode, executable: true, setup: option =>
        {
            option.DisabledDiagnostics.Remove(CheckedExceptionsAnalyzer.DiagnosticIdRedundantTypedCatchClause);
        });
    }

    [Fact]
    public async Task RemoveSingleCatchBlock_RemoveTry()
    {
        var testCode = /* lang=c#-test */  """
#nullable enable
using System;

public class Foo 
{
    public void Test() 
    {
        try
        {
            int x = 0;
            string z = "";
        }
        catch (FormatException formatException)
        {
        }
    }
}
""";

        var fixedCode = /* lang=c#-test */  """
#nullable enable
using System;

public class Foo 
{
    public void Test() 
    {
        int x = 0;
        string z = "";
    }
}
""";

        var expectedDiagnostic = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdRedundantTypedCatchClause)
                .WithArguments("FormatException")
                .WithSpan(13, 16, 13, 31);


        await Verifier.VerifyCodeFixAsync(testCode, [expectedDiagnostic], fixedCode, setup: option =>
        {
            option.DisabledDiagnostics.Remove(CheckedExceptionsAnalyzer.DiagnosticIdRedundantTypedCatchClause);
        });
    }

    [Fact]
    public async Task RemoveSingleCatchBlock_RemoveTry_GlobalStatement()
    {
        var testCode = /* lang=c#-test */  """
#nullable enable
using System;

try
{
    int x = 0;
    string z = "";
}
catch (FormatException formatException)
{
}
""";

        var fixedCode = /* lang=c#-test */  """
#nullable enable
using System;

int x = 0;
string z = "";
""";

        var expectedDiagnostic = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdRedundantTypedCatchClause)
                .WithArguments("FormatException")
                .WithSpan(9, 8, 9, 23);


        await Verifier.VerifyCodeFixAsync(testCode, [expectedDiagnostic], fixedCode, executable: true, setup: option =>
        {
            option.DisabledDiagnostics.Remove(CheckedExceptionsAnalyzer.DiagnosticIdRedundantTypedCatchClause);
        });
    }

    [Fact]
    public async Task RemoveCatchAlBlock()
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
catch
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

        var expectedDiagnostic = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdRedundantCatchAllClause)
                .WithSpan(14, 1, 14, 6);

        await Verifier.VerifyCodeFixAsync(testCode, [expectedDiagnostic], fixedCode, executable: true, setup: option =>
        {
            option.DisabledDiagnostics.Remove(CheckedExceptionsAnalyzer.DiagnosticIdRedundantTypedCatchClause);
        });
    }
}
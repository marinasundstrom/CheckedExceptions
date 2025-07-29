namespace Sundstrom.CheckedExceptions.Tests.CodeFixes.BugFixes;

using System.Threading.Tasks;

using Xunit;

using Verifier = CSharpCodeFixVerifier<CheckedExceptionsAnalyzer, SurroundWithTryCatchCodeFixProvider, Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

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

        await Verifier.VerifyCodeFixAsync(testCode, expectedDiagnostic, fixedCode, executable: true);
    }

    [Fact]
    public async Task AddTryCatch_Should_IncludeVariablesInScope()
    {
        var testCode = /* lang=c#-test */  """
using System;

string str = "";
int x = 0;
double d = 2;
var result = Foo(x);
#pragma warning disable THROW001 // Unhandled exception
Console.WriteLine(result);
#pragma warning restore THROW001
char ch = 'a';

[Throws(typeof(ArgumentException))]
int Foo(int arg) 
{
    throw new ArgumentException();
}
""";

        var fixedCode = /* lang=c#-test */  """
using System;

string str = "";
try
{
    int x = 0;
    double d = 2;
    var result = Foo(x);
#pragma warning disable THROW001 // Unhandled exception
    Console.WriteLine(result);
}
catch (ArgumentException argumentException)
{
}
#pragma warning restore THROW001
char ch = 'a';

[Throws(typeof(ArgumentException))]
int Foo(int arg) 
{
    throw new ArgumentException();
}
""";

        var expectedDiagnostic = Verifier.UnhandledException("ArgumentException")
            .WithSpan(6, 14, 6, 20);

        await Verifier.VerifyCodeFixAsync(testCode, expectedDiagnostic, fixedCode, executable: true);
    }
}
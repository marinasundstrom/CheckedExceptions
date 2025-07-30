namespace Sundstrom.CheckedExceptions.Tests.CodeFixes;

using System.Threading.Tasks;

using Microsoft.CodeAnalysis.Testing;

using Xunit;
using Xunit.Abstractions;

using Verifier = CSharpCodeFixVerifier<CheckedExceptionsAnalyzer, SurroundWithTryCatchCodeFixProvider, Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

public partial class SurroundWithTryCatchCodeFixProviderTests
{
    [Fact]
    public async Task MinimalTransitive()
    {
        var testCode = /* lang=c#-test */  """
using System;

var a = "1";
string str = a + "2";
var x = Parse(str);

[Throws(typeof(ArgumentException))]
int Parse(string str) => 0;
""";

        var fixedCode = /* lang=c#-test */  """
using System;

try
{
    var a = "1";
    string str = a + "2";
    var x = Parse(str);
}
catch (ArgumentException argumentException)
{
}

[Throws(typeof(ArgumentException))]
int Parse(string str) => 0;
""";

        var expectedDiagnostic = Verifier.UnhandledException("ArgumentException")
            .WithSpan(5, 9, 5, 19);

        await Verifier.VerifyCodeFixAsync(testCode, expectedDiagnostic, fixedCode, executable: true);
    }
}
namespace Sundstrom.CheckedExceptions.Tests.CodeFixes;

using System.Threading.Tasks;

using Microsoft.CodeAnalysis.Testing;

using Xunit;

using Verifier = CSharpCodeFixVerifier<CheckedExceptionsAnalyzer, MaterializeEnumerableCodeFixProvider, Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

public class MaterializeEnumerableCodeFixProviderTests
{
    [Fact]
    public async Task ReturnEnumerable()
    {
        var testCode = /* lang=c#-test */ """
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

IEnumerable<string> items = [];
IEnumerable<string> Get()
{
    var query = items.Where(x => int.Parse(x) > 0);
    return query;
}
""";

        var fixedCode = /* lang=c#-test */ """
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

IEnumerable<string> items = [];
IEnumerable<string> Get()
{
    var query = items.Where(x => int.Parse(x) > 0);
    return query.ToArray();
}
""";

        var expected = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdDeferredMustBeHandled)
            .WithArguments("IEnumerable<string>", "FormatException")
            .WithSpan(10, 12, 10, 17);
        var expected2 = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdDeferredMustBeHandled)
            .WithArguments("IEnumerable<string>", "OverflowException")
            .WithSpan(10, 12, 10, 17);

        await Verifier.VerifyCodeFixAsync(testCode, new[] { expected, expected2 }, fixedCode, executable: true, setup: opt =>
        {
            opt.DisabledDiagnostics.Add(CheckedExceptionsAnalyzer.DiagnosticIdImplicitlyDeclaredException);
        });
    }
}

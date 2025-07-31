namespace Sundstrom.CheckedExceptions.Tests.CodeFixes;

using System.Threading.Tasks;

using Xunit;
using Xunit.Abstractions;

using Verifier = CSharpCodeFixVerifier<CheckedExceptionsAnalyzer, AddThrowsDeclarationFromXmlDocCodeFixProvider, Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

public class AddThrowsDeclarationFromXmlDocCodeFixProviderTests
{
    [Fact]
    public async Task AddsThrowsAttribute_FromXmlDoc()
    {
        var testCode = /* lang=c#-test */  """
using System;

/// <exception cref="System.InvalidOperationException" />
void Foo()
{

}
""";

        var fixedCode = /* lang=c#-test */  """
using System;

/// <exception cref="System.InvalidOperationException" />
[Throws(typeof(InvalidOperationException))]
void Foo()
{

}
""";

        var expectedDiagnostic = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdXmlDocButNoThrows)
            .WithArguments("InvalidOperationException")
            .WithSpan(4, 6, 4, 9);

        await Verifier.VerifyCodeFixAsync(testCode, expectedDiagnostic, fixedCode, executable: true);
    }

    [Fact]
    public async Task AddsThrowsAttribute_FromXmlDoc2()
    {
        var testCode = /* lang=c#-test */  """
using System;

/// <exception cref="System.InvalidOperationException" />
/// <exception cref="System.ArgumentException" />
void Foo()
{
    
}
""";

        var fixedCode = /* lang=c#-test */  """
using System;

/// <exception cref="System.InvalidOperationException" />
/// <exception cref="System.ArgumentException" />
[Throws(typeof(ArgumentException), typeof(InvalidOperationException))]
void Foo()
{
    
}
""";

        var expectedDiagnostic = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdXmlDocButNoThrows)
            .WithArguments("InvalidOperationException")
            .WithSpan(5, 6, 5, 9);

        var expectedDiagnostic2 = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdXmlDocButNoThrows)
            .WithArguments("ArgumentException")
            .WithSpan(5, 6, 5, 9);

        await Verifier.VerifyCodeFixAsync(testCode, [expectedDiagnostic, expectedDiagnostic2], fixedCode, expectedIncrementalIterations: 2, executable: true);
    }

    [Fact]
    public async Task AddsThrowsAttribute_ToProperty_FromXmlDoc()
    {
        var testCode = /* lang=c#-test */  """
using System;

public class Test 
{
    /// <exception cref="System.InvalidOperationException" />
    public int Foo
    {
        get;
    }
}
""";

        var fixedCode = /* lang=c#-test */  """
using System;

public class Test 
{
    /// <exception cref="System.InvalidOperationException" />
    public int Foo
    {
        [Throws(typeof(InvalidOperationException))]
        get;
    }
}
""";

        var expectedDiagnostic = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdXmlDocButNoThrows)
            .WithArguments("InvalidOperationException")
            .WithSpan(8, 9, 8, 12);

        await Verifier.VerifyCodeFixAsync(testCode, expectedDiagnostic, fixedCode);
    }

    [Fact]
    public async Task AddsThrowsAttribute_ToProperty_FromXmlDoc2()
    {
        var testCode = /* lang=c#-test */  """
using System;

public class Test 
{
    /// <exception cref="InvalidOperationException" />
    public int Foo => 0;
}
""";

        var fixedCode = /* lang=c#-test */  """
using System;

public class Test 
{
    /// <exception cref="InvalidOperationException" />
    [Throws(typeof(InvalidOperationException))]
    public int Foo => 0;
}
""";

        var expectedDiagnostic = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdXmlDocButNoThrows)
            .WithArguments("InvalidOperationException")
            .WithSpan(6, 16, 6, 19);

        await Verifier.VerifyCodeFixAsync(testCode, expectedDiagnostic, fixedCode, expectedIncrementalIterations: 1);
    }
}
namespace Sundstrom.CheckedExceptions.Tests.CodeFixes;

using System.Threading.Tasks;

using Xunit;
using Xunit.Abstractions;

using Verifier = CSharpCodeFixVerifier<CheckedExceptionsAnalyzer, AddThrowsDeclarationFromBaseMemberCodeFixProvider, Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

public class AddThrowsDeclarationFromBaseMemberCodeFixProviderTests
{
    [Fact]
    public async Task AddThrowsAttribute_FromMember1()
    {
        var testCode = /* lang=c#-test */  """
using System;

public class TestBase
{
    [Throws(typeof(InvalidOperationException))]
    public virtual bool Foo() => throw new InvalidOperationException();
}

public class TestDerive : TestBase
{
    public override bool Foo() => false;
}
""";

        var fixedCode = /* lang=c#-test */  """
using System;

public class TestBase
{
    [Throws(typeof(InvalidOperationException))]
    public virtual bool Foo() => throw new InvalidOperationException();
}

public class TestDerive : TestBase
{
    [Throws(typeof(InvalidOperationException))]
    public override bool Foo() => false;
}
""";

        var expectedDiagnostic = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdMissingThrowsFromBaseMember)
            .WithArguments("TestBase.Foo()", "InvalidOperationException")
            .WithSpan(11, 26, 11, 29);

        await Verifier.VerifyCodeFixAsync(testCode, expectedDiagnostic, fixedCode);
    }

    [Fact]
    public async Task AddThrowsAttribute_FromMember2()
    {
        var testCode = /* lang=c#-test */  """
using System;

public class TestBase
{
    [Throws(typeof(InvalidOperationException), typeof(ArgumentException))]
    public virtual bool Foo() => throw new InvalidOperationException();
}

public class TestDerive : TestBase
{
    [Throws(typeof(InvalidOperationException))]
    public override bool Foo() => false;
}
""";

        var fixedCode = /* lang=c#-test */  """
using System;

public class TestBase
{
    [Throws(typeof(InvalidOperationException), typeof(ArgumentException))]
    public virtual bool Foo() => throw new InvalidOperationException();
}

public class TestDerive : TestBase
{
    [Throws(typeof(InvalidOperationException), typeof(ArgumentException))]
    public override bool Foo() => false;
}
""";

        var expectedDiagnostic = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdMissingThrowsFromBaseMember)
            .WithArguments("TestBase.Foo()", "ArgumentException")
            .WithSpan(12, 26, 12, 29);

        await Verifier.VerifyCodeFixAsync(testCode, expectedDiagnostic, fixedCode);
    }

    [Fact]
    public async Task AddThrowsAttribute_FromMember_OnProperty()
    {
        var testCode = /* lang=c#-test */  """
using System;

public class TestBase
{
    [Throws(typeof(InvalidOperationException))]
    public virtual bool Foo => throw new InvalidOperationException();
}

public class TestDerive : TestBase
{
    public override bool Foo => false;
}
""";

        var fixedCode = /* lang=c#-test */  """
using System;

public class TestBase
{
    [Throws(typeof(InvalidOperationException))]
    public virtual bool Foo => throw new InvalidOperationException();
}

public class TestDerive : TestBase
{
    [Throws(typeof(InvalidOperationException))]
    public override bool Foo => false;
}
""";

        var expectedDiagnostic = Verifier.Diagnostic(CheckedExceptionsAnalyzer.DiagnosticIdMissingThrowsFromBaseMember)
            .WithArguments("TestBase.get_Foo()", "InvalidOperationException")
            .WithSpan(11, 5, 11, 39);

        await Verifier.VerifyCodeFixAsync(testCode, expectedDiagnostic, fixedCode);
    }
}
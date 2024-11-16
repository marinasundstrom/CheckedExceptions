using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

namespace CheckedExceptions.Test;

using Verifier = CSharpAnalyzerVerifier<CheckedExceptionsAnalyzer, DefaultVerifier>;

partial class CheckedExceptionsAnalyzerTests
{
    [Fact]
    public async Task ExceptionInPropertyGetter_ShouldReportDiagnostic()
    {
        var test = @"
using System;

public class TestClass
{
    public int Property
    {
        get
        {
            throw new InvalidOperationException();
        }
    }
}";

        var expected = Verifier.Diagnostic("THROW001")
            .WithSpan(10, 13, 10, 51)
            .WithArguments("InvalidOperationException");

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ExceptionInPropertySetter_ShouldReportDiagnostic()
    {
        var test = @"
using System;

public class TestClass
{
    private int _value;
    public int Property
    {
        get => _value;
        set
        {
            throw new InvalidOperationException();
        }
    }
}";

        var expected = Verifier.Diagnostic("THROW001")
            .WithSpan(12, 13, 12, 51)
            .WithArguments("InvalidOperationException");

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ExceptionInIndexer_ShouldReportDiagnostic()
    {
        var test = @"
using System;

public class TestClass
{
    public int this[int index]
    {
        get
        {
            throw new InvalidOperationException();
        }
        set
        {
            throw new ArgumentException();
        }
    }
}";

        var expected1 = Verifier.Diagnostic("THROW001")
            .WithSpan(10, 13, 10, 51)
            .WithArguments("InvalidOperationException");

        var expected2 = Verifier.Diagnostic("THROW001")
            .WithSpan(14, 13, 14, 43)
            .WithArguments("ArgumentException");

        await Verifier.VerifyAnalyzerAsync(test, expected1, expected2);
    }

    [Fact]
    public async Task ExceptionInLocalFunction_ShouldReportDiagnostic()
    {
        var test = @"
using System;

public class TestClass
{
    public void TestMethod()
    {
        void LocalFunction()
        {
            throw new InvalidOperationException();
        }

        LocalFunction();
    }
}";

        var expected = Verifier.Diagnostic("THROW001")
            .WithSpan(10, 13, 10, 51)
            .WithArguments("InvalidOperationException");

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ExceptionInLambda_ShouldReportDiagnostic()
    {
        var test = @"
using System;

public class TestClass
{
    public void TestMethod()
    {
        Action action = () => throw new InvalidOperationException();
        action();
    }
}";

        var expected = Verifier.Diagnostic("THROW001")
            .WithSpan(8, 31, 8, 68)
            .WithArguments("InvalidOperationException");

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ExceptionInLambdaCalledFromMethod_ShouldReportDiagnostic()
    {
        var test = @"
using System;

public class TestClass
{
    public void TestMethod()
    {
        Action action = () => { throw new InvalidOperationException(); };
        action();
    }
}";

        var expected = Verifier.Diagnostic("THROW001")
            .WithSpan(8, 33, 8, 71)
            .WithArguments("InvalidOperationException");

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ExceptionInEventHandler_ShouldReportDiagnostic()
    {
        var test = @"
using System;

public class TestClass
{
    public event EventHandler MyEvent;

    public void TriggerEvent()
    {
        MyEvent += (s, e) => throw new InvalidOperationException();
        MyEvent?.Invoke(this, EventArgs.Empty);
    }
}";

        var expected = Verifier.Diagnostic("THROW001")
            .WithSpan(10, 30, 10, 67)
            .WithArguments("InvalidOperationException");

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ExceptionInStaticConstructor_ShouldReportDiagnostic()
    {
        var test = @"
using System;

public class TestClass
{
    static TestClass()
    {
        throw new InvalidOperationException();
    }
}";

        var expected = Verifier.Diagnostic("THROW001")
            .WithSpan(8, 9, 8, 47)
            .WithArguments("InvalidOperationException");

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ExceptionInInstanceConstructor_ShouldReportDiagnostic()
    {
        var test = @"
using System;

public class TestClass
{
    public TestClass()
    {
        throw new InvalidOperationException();
    }
}";

        var expected = Verifier.Diagnostic("THROW001")
            .WithSpan(8, 9, 8, 47)
            .WithArguments("InvalidOperationException");

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }

}

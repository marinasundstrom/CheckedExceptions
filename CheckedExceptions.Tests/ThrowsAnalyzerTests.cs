using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace CheckedExceptions.Test;

using Verifier = CSharpAnalyzerVerifier<CheckedExceptions, DefaultVerifier>;

public class CheckedExceptionsTests
{
    [Fact]
    public async Task MethodWithoutHandlingThrowsException_ShouldTriggerWarning()
    {
        var testCode = @"
using System;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Delegate, AllowMultiple = true)]
public class ThrowsAttribute : Attribute
{
    public Type ExceptionType { get; }

    public ThrowsAttribute(Type exceptionType)
    {
        if (!typeof(Exception).IsAssignableFrom(exceptionType))
            throw new ArgumentException(""ExceptionType must be an Exception type."");

        ExceptionType = exceptionType;
    }
}

public class DataFetcher
{
    [Throws(typeof(NullReferenceException))]
    public void FetchData()
    {
        throw new NullReferenceException(""Data source is null."");
    }
}

public class Example
{
    public void ProcessData()
    {
        var fetcher = new DataFetcher();
        fetcher.FetchData();
    }
}";

        var expectedDiagnostic = new DiagnosticResult("THROW001", DiagnosticSeverity.Warning)
            .WithMessage("Method 'FetchData' throws exception 'NullReferenceException' which is not handled")
            .WithSpan(32, 9, 32, 28); // Span points to fetcher.FetchData();

        await Verifier.VerifyAnalyzerAsync(testCode, expectedDiagnostic);
    }
}
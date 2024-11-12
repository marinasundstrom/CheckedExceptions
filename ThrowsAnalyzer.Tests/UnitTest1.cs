using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace ThrowsAnalyzer.Test;

public class ThrowsAnalyzerTests : CSharpAnalyzerTest<ThrowsAnalyzer, DefaultVerifier>
{
    [Fact]
    public async Task MethodWithoutHandlingThrowsException_ShouldTriggerWarning()
    {
        var testCode = @"
using System;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
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
            .WithSpan(22, 9, 22, 27); // Span points to fetcher.FetchData();

        await AnalyzerVerifier<ThrowsAnalyzer, ThrowsAnalyzerTests,
            DefaultVerifier>.VerifyAnalyzerAsync(testCode, expectedDiagnostic);
    }
}
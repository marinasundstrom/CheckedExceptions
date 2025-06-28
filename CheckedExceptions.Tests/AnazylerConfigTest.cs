using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;

namespace Sundstrom.CheckedExceptions.Tests;

using Verifier = CSharpAnalyzerVerifier<CheckedExceptionsAnalyzer, DefaultVerifier>;

public partial class AnaylzerConfigTest
{
    [Fact(DisplayName = "Declaring exception as ignored should not report unhandled")]
    public async Task DeclaringExceptionAsIgnored_ShouldNotReportInfoDiagnostic()
    {
        var test = /* lang=c#-test */ """
            using System;

            public class TestClass
            {
                public void TestMethod2()
                {
                    throw new NotImplementedException();
                }
            }
            """;

        await Verifier.VerifyAnalyzerAsync2(test);
    }


    [Fact(DisplayName = "Declaring exception as informational should report info diagnostic")]
    public async Task DeclaringExceptionAsInformational_ShouldReportInfoDiagnostic()
    {
        var test = /* lang=c#-test */ """
            using System;

            public class TestClass
            {
                public void TestMethod2()
                {
                    Console.WriteLine("Hello");
                }
            }
            """;

        var expected = Verifier.Informational("IOException")
            .WithSpan(7, 17, 7, 35);

        await Verifier.VerifyAnalyzerAsync2(test, expected);
    }

    [Fact(DisplayName = "Declaring exception as informational should report info diagnostic")]
    public async Task DeclaringExceptionAsInformational_ShouldReportInfoDiagnostic2()
    {
        var test = /* lang=c#-test */ """
            using System;

            public class TestClass
            {
                public void TestMethod2()
                {
                    try 
                    {
                        Console.WriteLine("Hello");
                    }
                    catch 
                    {
                        throw;
                    }
                }
            }
            """;

        var expected2 = Verifier.Informational("IOException")
            .WithSpan(9, 21, 9, 39);

        await Verifier.VerifyAnalyzerAsync2(test, expected2);
    }
}
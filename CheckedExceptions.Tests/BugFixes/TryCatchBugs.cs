using Microsoft.CodeAnalysis.Testing;

namespace Sundstrom.CheckedExceptions.Tests.BugFixes;

using Verifier = CSharpAnalyzerVerifier<CheckedExceptionsAnalyzer, DefaultVerifier>;

public partial class TryCatchBugs
{
    [Fact]
    public async Task Should_ReportDiagnostics_ForUnhandledThrowInCatch()
    {
        var test = /* lang=c#-test */ """
        using System;

        public class Test
        {
            [Throws(typeof(InvalidOperationException))]
            public void Foo1() 
            {
                throw new InvalidOperationException();
            }

            public void Foo()
            {
                try
                {
                    Foo1();
                }
                catch (InvalidOperationException e)
                {
                    throw new ArgumentException();
                }
            }
        }
        """;

        var expected = Verifier.IsThrown("ArgumentException")
            .WithSpan(19, 13, 19, 43);

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task Should_ReportDiagnostics_ForUnhandledThrowInCatchInCatchOfSameType()
    {
        var test = /* lang=c#-test */ """
        using System;

        public class Test
        {
            [Throws(typeof(InvalidOperationException))]
            public void Foo1() 
            {
                throw new InvalidOperationException();
            }

            public void Foo()
            {
                try
                {
                    Foo1();
                }
                catch (InvalidOperationException e)
                {
                    throw new InvalidOperationException();
                }
            }
        }
        """;

        var expected = Verifier.IsThrown("InvalidOperationException")
            .WithSpan(19, 13, 19, 51);

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task Should_ReportDiagnostics_ForUnhandledThrowingMethodFooInCatch()
    {
        var test = /* lang=c#-test */ """
        using System;
        using System.IO;

        public class Test
        {
            [Throws(typeof(InvalidOperationException))]
            public void Foo1() 
            {
                throw new InvalidOperationException();
            }

            public void Foo()
            {
                try
                {
                    Foo1();
                }
                catch (InvalidOperationException e)
                {
                    Foo1();
                }
            }
        }
        """;

        var expected = Verifier.MightBeThrown("InvalidOperationException")
            .WithSpan(20, 13, 20, 19);

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task Should_ReportDiagnostics_ForUnhandledThrowingMethodFooInCatchOfSameType()
    {
        var test = /* lang=c#-test */ """
        using System;
        using System.IO;

        public class Test
        {
            [Throws(typeof(InvalidOperationException))]
            public void Foo1() 
            {
                throw new InvalidOperationException();
            }

            public void Foo()
            {
                try
                {
                    Foo1();
                }
                catch (InvalidOperationException e)
                {
                    Foo1();
                }
            }
        }
        """;

        var expected = Verifier.MightBeThrown("InvalidOperationException")
            .WithSpan(20, 13, 20, 19);

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }
}
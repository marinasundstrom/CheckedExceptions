using Microsoft.CodeAnalysis.Testing;

namespace Sundstrom.CheckedExceptions.Tests;

using Verifier = CSharpAnalyzerVerifier<CheckedExceptionsAnalyzer, DefaultVerifier>;

public partial class TryCatchTest
{
    [Fact]
    public async Task Should_ReportDiagnostic_ForExceptionThrownDirectlyInMethod()
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
                Foo1();
            }
        }
        """;

        var expected = Verifier.UnhandledException("InvalidOperationException")
            .WithSpan(14, 9, 14, 15);


        await Verifier.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task Should_ReportDiagnostic_ForExceptionThrownDirectlyInCatchBlock()
    {
        var test = /* lang=c#-test */ """
        using System;
        using System.IO;

        public class Test
        {
            public void Foo()
            {
                throw new InvalidOperationException();
            }
        }
        """;

        var expected = Verifier.UnhandledException("InvalidOperationException")
            .WithSpan(8, 9, 8, 47);

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task Should_ReportDiagnostics_ForMultipleExceptionsThrownInMethods()
    {
        var test = /* lang=c#-test */ """
        using System;
        using System.IO;

        public class Test
        {
            [Throws(typeof(ArgumentNullException))]
            public void Foo1() 
            {
                throw new ArgumentNullException();
            }

            public void Foo()
            {
                Foo1(); 

                throw new InvalidOperationException();
            }
        }
        """;

        var expected1 = Verifier.UnhandledException("ArgumentNullException")
            .WithSpan(14, 9, 14, 15);

        var expected2 = Verifier.UnhandledException("InvalidOperationException")
            .WithSpan(16, 9, 16, 47);

        await Verifier.VerifyAnalyzerAsync(test, [expected1, expected2]);
    }

    [Fact]
    public async Task Should_ReportDiagnostics_ForMultipleExceptionsThrownInSequentialCalls()
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

            [Throws(typeof(ArgumentNullException))]
            public void Foo2() 
            {
                throw new ArgumentNullException();
            }

            public void Foo()
            {
                Foo1();
                Foo2();
            }
        }
        """;

        var expected1 = Verifier.UnhandledException("InvalidOperationException")
            .WithSpan(20, 9, 20, 15);

        var expected2 = Verifier.UnhandledException("ArgumentNullException")
            .WithSpan(21, 9, 21, 15);

        await Verifier.VerifyAnalyzerAsync(test, [expected1, expected2]);
    }

    [Fact]
    public async Task Should_ReportDiagnostics_ForExceptionUncaughtWithinCatchBlock()
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

            [Throws(typeof(ArgumentNullException))]
            public void Foo2() 
            {
                throw new ArgumentNullException();
            }

            public void Foo()
            {
                try
                {
                    Foo1();
                    Foo2();
                }
                catch (InvalidOperationException)
                {
                    // Handle InvalidOperationException
                }
            }
        }
        """;

        var expected = Verifier.UnhandledException("ArgumentNullException")
            .WithSpan(23, 13, 23, 19);

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task Should_NotReportDiagnostics_WhenAllExceptionsAreCaught()
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

            [Throws(typeof(ArgumentNullException))]
            public void Foo2() 
            {
                throw new ArgumentNullException();
            }

            public void Foo()
            {
                try
                {
                    Foo1();
                    Foo2();
                }
                catch (InvalidOperationException)
                {
                    // Handle InvalidOperationException
                }
                catch (ArgumentNullException)
                {
                    // Handle ArgumentNullException
                }
            }
        }
        """;

        await Verifier.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task Should_ReportDiagnostics_WhenExceptionsAreThrownAndRethrown()
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

            [Throws(typeof(ArgumentNullException))]
            public void Foo2() 
            {
                throw new ArgumentNullException();
            }

            public void Foo()
            {
                try
                {
                    Foo1();
                    Foo2();
                }
                catch 
                {
                    throw;
                }
            }
        }
        """;

        var expected1 = Verifier.UnhandledException("InvalidOperationException")
            .WithSpan(27, 13, 27, 19);

        var expected2 = Verifier.UnhandledException("ArgumentNullException")
            .WithSpan(27, 13, 27, 19);

        await Verifier.VerifyAnalyzerAsync(test, [expected1, expected2]);
    }

    [Fact]
    public async Task Should_ReportDiagnostics_ForNestedTryCatchWithUncaughtExceptions()
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

            [Throws(typeof(ArgumentNullException))]
            public void Foo2() 
            {
                throw new ArgumentNullException();
            }

            public void Foo()
            {
                try
                {
                    Foo1();
                    Foo2();
                }
                catch 
                {
                    try
                    {
                        Foo1();
                        Foo2();
                    }
                    catch 
                    {
                        throw;
                    }
                }
            }
        }
        """;

        var expected1 = Verifier.UnhandledException("InvalidOperationException")
            .WithSpan(34, 17, 34, 23);

        var expected2 = Verifier.UnhandledException("ArgumentNullException")
            .WithSpan(34, 17, 34, 23);

        await Verifier.VerifyAnalyzerAsync(test, [expected1, expected2]);
    }
}
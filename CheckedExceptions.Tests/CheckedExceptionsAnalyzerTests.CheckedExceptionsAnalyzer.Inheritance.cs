using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;

namespace Sundstrom.CheckedExceptions.Tests;

using Verifier = CSharpAnalyzerVerifier<CheckedExceptionsAnalyzer, DefaultVerifier>;

partial class CheckedExceptionsAnalyzerTests
{
    [Fact]
    public async Task ThrowsCompatibleWithBase_ShouldNotReportDiagnostic()
    {
        var test = /* lang=c#-test */ """
        using System;
        using Sundstrom.CheckedExceptions;

        public class BaseService
        {
            [Throws(typeof(Exception))]
            public virtual void DoWork() { }
        }

        public class MyService : BaseService
        {
            [Throws(typeof(ArgumentException))] // ✅ Is sub-class of Exception
            public override void DoWork()
            {
                throw new ArgumentException();
            }
        }
        """;

        await Verifier.VerifyAnalyzerAsync(test, s =>
        {
            s.DisabledDiagnostics.Add("THROW003");
        });
    }

    [Fact]
    public async Task ThrowsIncompatibleWithBase_ShouldReportDiagnostic()
    {
        var test = /* lang=c#-test */ """
        using System;
        using System.IO;
        using Sundstrom.CheckedExceptions;

        public class BaseService
        {
            [Throws(typeof(InvalidOperationException))]
            public virtual void DoWork() { }
        }

        public class MyService : BaseService
        {
            [Throws(typeof(IOException))] // ❌ Not compatible
            public override void DoWork()
            {
                throw new IOException();
            }
        }
        """;

        var expected = Verifier.Diagnostic("THROW006")
            .WithSpan(8, 25, 8, 31)
            .WithArguments("BaseService.DoWork", "IOException");

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ThrowsIncompatibleWithInterface_ShouldReportDiagnostic()
    {
        var test = /* lang=c#-test */ """
        using System;
        using System.IO;
        using System.Net.Sockets;
        using Sundstrom.CheckedExceptions;

        public interface IOperation
        {
            [Throws(typeof(IOException))]
            void Execute();
        }

        public class NetworkOperation : IOperation
        {
            [Throws(typeof(SocketException))] // <--
            public void Execute()
            {
                throw new SocketException();
            }
        }
        """;

        var expected = Verifier.Diagnostic("THROW006")
            .WithSpan(9, 10, 9, 17)
            .WithArguments("IOperation.Execute", "SocketException");

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ThrowsIncompatibleInGetterOverride_ShouldReportDiagnostic()
    {
        var test = /* lang=c#-test */ """
        using System;
        using System.IO;
        using Sundstrom.CheckedExceptions;

        public class Base
        {
            public virtual int Value
            {
                [Throws(typeof(InvalidOperationException))]
                get => 42;
            }
        }

        public class Derived : Base
        {
            public override int Value
            {
                [Throws(typeof(IOException))] // ❌ Not compatible
                get => throw new IOException();
            }
        }
        """;

        var expected = Verifier.Diagnostic("THROW006")
            .WithSpan(10, 9, 10, 12)
            .WithArguments("Base.get_Value", "IOException");

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ThrowsIncompatibleInEventAddOverride_ShouldReportDiagnostic()
    {
        var test = /* lang=c#-test */ """
        using System;
        using Sundstrom.CheckedExceptions;

        public class Base
        {
            public virtual event EventHandler Something
            {
                [Throws(typeof(NotSupportedException))]
                add { }
                remove { }
            }
        }

        public class Derived : Base
        {
            public override event EventHandler Something
            {
                [Throws(typeof(UnauthorizedAccessException))] // ❌ Not compatible
                add { throw new UnauthorizedAccessException(); }
                remove { }
            }
        }
        """;

        var expected = Verifier.Diagnostic("THROW006")
            .WithSpan(9, 9, 9, 12)
            .WithArguments("Base.add_Something", "UnauthorizedAccessException");

        await Verifier.VerifyAnalyzerAsync(test, expected);
    }

    [Fact]
    public async Task ThrowsCompatibleInGetterOverride_ShouldNotReportDiagnostic()
    {
        var test = /* lang=c#-test */ """
        using System;
        using Sundstrom.CheckedExceptions;

        public class Base
        {
            public virtual int Value
            {
                [Throws(typeof(Exception))]
                get => 42;
            }
        }

        public class Derived : Base
        {
            public override int Value
            {
                [Throws(typeof(ArgumentException))] // ✅ Is sub-class of Exception
                get => throw new ArgumentException();
            }
        }
        """;

        await Verifier.VerifyAnalyzerAsync(test, s =>
        {
            s.DisabledDiagnostics.Add("THROW003");
        });
    }

    [Fact]
    public async Task ThrowsCompatibleInEventAddOverride_ShouldNotReportDiagnostic()
    {
        var test = /* lang=c#-test */ """
        using System;
        using Sundstrom.CheckedExceptions;

        public class Base
        {
            public virtual event EventHandler Something
            {
                [Throws(typeof(Exception))]
                add { }
                remove { }
            }
        }

        public class Derived : Base
        {
            public override event EventHandler Something
            {
                [Throws(typeof(InvalidOperationException))] // ✅ Is sub-class of Exception
                add { throw new InvalidOperationException(); }
                remove { }
            }
        }
        """;

        await Verifier.VerifyAnalyzerAsync(test, s =>
        {
            s.DisabledDiagnostics.Add("THROW003");
        });
    }
}
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

        var expected = Verifier.MissingThrowsOnBaseMember("IOException", "BaseService.DoWork()")
            .WithSpan(14, 26, 14, 32);

        var expected2 = Verifier.MissingThrowsFromBaseMember("InvalidOperationException", "BaseService.DoWork()")
            .WithSpan(14, 26, 14, 32);

        await Verifier.VerifyAnalyzerAsync(test, expected, expected2);
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
            [Throws(typeof(SocketException))]
            public void Execute()
            {
                throw new SocketException();
            }
        }
        """;

        var expected = Verifier.MissingThrowsOnBaseMember("SocketException", "IOperation.Execute()")
            .WithSpan(15, 17, 15, 24);

        var expected2 = Verifier.MissingThrowsFromBaseMember("IOException", "IOperation.Execute()")
            .WithSpan(15, 17, 15, 24);

        await Verifier.VerifyAnalyzerAsync(test, expected, expected2);
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

        var expected = Verifier.MissingThrowsOnBaseMember("IOException", "Base.get_Value()")
            .WithSpan(19, 9, 19, 12);

        var expected2 = Verifier.MissingThrowsFromBaseMember("InvalidOperationException", "Base.get_Value()")
            .WithSpan(19, 9, 19, 12);

        await Verifier.VerifyAnalyzerAsync(test, expected, expected2);
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

        var expected = Verifier.MissingThrowsOnBaseMember("UnauthorizedAccessException", "Base.add_Something(EventHandler)")
            .WithSpan(19, 9, 19, 12);

        var expected2 = Verifier.MissingThrowsFromBaseMember("NotSupportedException", "Base.add_Something(EventHandler)")
            .WithSpan(19, 9, 19, 12);

        await Verifier.VerifyAnalyzerAsync(test, expected, expected2);
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
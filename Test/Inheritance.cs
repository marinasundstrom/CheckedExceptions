using System.Net.Sockets;

namespace Test;

public interface IOperation
{
    [Throws(typeof(IOException))]
    [Throws(typeof(InvalidOperationException))]
    void Execute();

    bool Foo
    {
        [Throws(typeof(InvalidDataException))]
        [Throws(typeof(IOException))]
        get;
    }
}

public class FileOperation : IOperation
{
    public bool Foo
    {
        [Throws(typeof(InvalidDataException))]
        [Throws(typeof(IOException))]
        get;
    }

    [Throws(typeof(FileNotFoundException))]  // ✅ Sub-class of IOException
    [Throws(typeof(InvalidOperationException))] // ✅ Exact match
    public void Execute()
    {
        throw new FileNotFoundException();
    }
}

public class NetworkOperation : IOperation
{
    public bool Foo => throw new NotImplementedException();

    [Throws(typeof(SocketException))] // ❌ SocketException is NOT a sub-class of IOException
    public void Execute()
    {
        throw new SocketException();
    }
}

public interface IOperation2
{
    //[Throws(typeof(IOException))]
    void Run();
}

public class Impl : IOperation2
{
    [Throws(typeof(IOException))]
    public void Run() => throw new IOException();
}

public class TestBase
{
    [Throws(typeof(InvalidDataException))]
    public virtual bool Foo => throw new InvalidDataException();

    [Throws(typeof(InvalidOperationException))]
    public virtual bool Foo2 => throw new InvalidOperationException();
}

public class TestDerive : TestBase
{
    [Throws(typeof(InvalidDataException))]
    public override bool Foo { get; }

    public override bool Foo2
    {
        [Throws(typeof(ArgumentException))]
        get => true;
    }
}
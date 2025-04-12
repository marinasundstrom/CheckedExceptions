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
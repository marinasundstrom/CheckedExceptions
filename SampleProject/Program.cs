public interface IOperation
{
    [Throws(typeof(InvalidDataException))]
    bool Foo { get; }
}

public class FileOperation : IOperation
{
    public bool Foo
    {
        [Throws(typeof(InvalidDataException))]
        get => true;
    }
}
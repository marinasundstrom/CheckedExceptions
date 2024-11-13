namespace Test;

public class EventPublisher
{
    public delegate void CustomEventHandler(object sender, EventArgs e);

    private CustomEventHandler handlers;

    public event CustomEventHandler CustomEvent
    {
        [Throws(typeof(InvalidOperationException))]
        add
        {
            if (value == null)
                throw new InvalidOperationException("Cannot add a null handler.");

            handlers += value;
        }

        [Throws(typeof(InvalidOperationException))]
        remove
        {
            if (value == null)
                throw new InvalidOperationException("Cannot remove a null handler.");

            handlers -= value;
        }
    }

    public event CustomEventHandler CustomEvent2
    {
        [Throws(typeof(InvalidOperationException))]
        add
        {
            if (value == null)
                throw new InvalidOperationException("Cannot add a null handler.");

            handlers += value;
        }

        [Throws(typeof(InvalidOperationException))]
        remove
        {
            Foo();

            handlers -= value;
        }
    }

    [Throws(typeof(InvalidOperationException))]
    public void Foo()
    {
        throw new InvalidOperationException();
    }
}

public class TestEvent
{
    public void Test()
    {
        var publisher = new EventPublisher();

        publisher.CustomEvent += null; // Should trigger a warning unless handled or declared
    }
}
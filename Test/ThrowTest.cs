using System;

public class ThrowingObject
{
    [Throws(typeof(InvalidOperationException))]
    public ThrowingObject()
    {
        throw new InvalidOperationException();
    }
}

public class TestClass
{
    public void ExplicitNew()
    {
        ThrowingObject obj = new ThrowingObject(); // ❗ Expect diagnostic
    }

    public void TargetTypedNew()
    {
        ThrowingObject obj = new(); // ❗ Expect diagnostic
    }
}
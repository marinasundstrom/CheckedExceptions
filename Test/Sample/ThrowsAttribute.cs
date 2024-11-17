namespace ECommerceSystem;

// Your version of ThrowsAttribute
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Delegate, AllowMultiple = true)]
public class ThrowsAttribute : Attribute
{
    public Type ExceptionType { get; }

    public ThrowsAttribute(Type exceptionType)
    {
        if (!typeof(Exception).IsAssignableFrom(exceptionType))
#pragma warning disable THROW001 // Unhandled exception thrown
            throw new ArgumentException("ExceptionType must be an Exception type.");
#pragma warning restore THROW001 // Unhandled exception thrown

        ExceptionType = exceptionType;
    }
}

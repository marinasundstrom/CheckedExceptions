// Define ThrowsAttribute within System namespace for testing purposes
namespace System;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Assembly | AttributeTargets.Module, AllowMultiple = true)]
public class ThrowsAttribute : Attribute
{
    public ThrowsAttribute(Type exceptionType)
    {
    }

    // If your attribute supports multiple exception types via params, include an appropriate constructor
    public ThrowsAttribute(params Type[] exceptionTypes)
    {
    }
}
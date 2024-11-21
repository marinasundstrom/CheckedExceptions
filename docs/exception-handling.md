## Exception Handling

### Analyzer

The analyzer is designed to streamline exception management by performing two primary tasks:

1. **Identifying Exception Sources**: It detects `throw` statements or method calls where exceptions may be thrown or propagated.
2. **Reporting Diagnostics**: It flags unhandled exceptions, prompting developers to either handle them explicitly or declare their propagation.

### Defining the `ThrowsAttribute`

The analyzer can automatically detect exceptions being thrown, but to maximize its utility, you need to define and use the `ThrowsAttribute`. This attribute allows you to annotate methods, constructors, or delegates with the exceptions they might throw, enabling the analyzer to generate more accurate diagnostics.

```csharp
using System;

namespace Sundstrom.CheckedExceptions
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Delegate, AllowMultiple = true)]
    public class ThrowsAttribute : Attribute
    {
        public Type ExceptionType { get; }

        public ThrowsAttribute(Type exceptionType)
        {
            if (!typeof(Exception).IsAssignableFrom(exceptionType))
                throw new ArgumentException("ExceptionType must be an Exception type.");

            ExceptionType = exceptionType;
        }
    }
}
```

### Handling Exceptions

#### Example: Unhandled Exception

A simple `throw` statement generates a diagnostic indicating the exception is unhandled:

```csharp
public void Foo()
{
    throw new InvalidOperationException();
}
```

To address this, you can catch the exception:

```csharp
public void Foo()
{
    try
    {
        throw new InvalidOperationException();
    }
    catch (InvalidOperationException)
    {
        // Handle the exception
    }
}
```

Alternatively, you can declare that the method propagates the exception using `ThrowsAttribute`. If not handled at the call site, this will trigger a diagnostic, encouraging explicit handling. Propagation should generally be a last resort:

```csharp
[Throws(typeof(InvalidOperationException))]
public void Foo()
{
    throw new InvalidOperationException();
}
```

The caller can then handle the propagated exception:

```csharp
try
{
    Foo();
}
catch (InvalidOperationException)
{
    // Handle the exception
}
```

The analyzer provides code fixes for handling or propagating exceptions.

#### Example: Multiple Exceptions

The analyzer supports multiple `ThrowsAttribute` annotations:

```csharp
[Throws(typeof(ArgumentOutOfRangeException))]
[Throws(typeof(InvalidOperationException))]
public void Foo()
{
    // Code that might throw exceptions
}

try
{
    Foo();
}
catch (ArgumentException)
{
    // Handles ArgumentOutOfRangeException
}
catch (InvalidOperationException)
{
    // Handle InvalidOperationException
}
```

The analyzer respects exception inheritance hierarchies. For example, catching `ArgumentException` covers exceptions like `ArgumentOutOfRangeException` that inherit from it.

### Documentation XML

To ensure compatibility with libraries lacking `ThrowsAttribute annotations`, the analyzer uses XML documentation to infer potential exceptions. These annotations work alongside the `ThrowsAttribute` and provide fallback diagnostics.

However, unannotated libraries rely on developers' manual checks and are less reliable than annotated ones.

The analyzer also considers nullability when handling cases involving `ArgumentNullException`. Diagnostics are adjusted to align with the rules of nullable contexts. More on that below.


#### Example: Unannotated Library

Consider this from a library that has not been annotated:

```csharp
#nullable disable

using System;

public class TestClass
{
    private int _value;

    /// <summary>
    /// A property
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the value is less than 42.
    /// </exception>
    public int Value
    {
        get => _value;
        set
        {
            if (value < 42)
            {
                throw new ArgumentOutOfRangeException("Value can't be less than 42");
            }
            _value = value;
        }
    }

    /// <summary>
    /// A method
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="value"/> is null.
    /// </exception>
    public void Process(string value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        _value = int.Parse(value); // May throw additional exceptions
    }
}
```

#### Example: Utilizing the Unannotated Library

Here’s how the library can be utilized in a project where the analyzer is installed, with nullable annotations explicitly disabled:

```csharp
#nullable disable

TestClass test = new TestClass();

try
{
    test.Process(null); // Throws ArgumentNullException
}
catch (ArgumentNullException)
{
    // Handle ArgumentNullException
}

try
{
    test.Value = 30; // Throws ArgumentOutOfRangeException
}
catch (ArgumentOutOfRangeException)
{
    // Handle ArgumentOutOfRangeException
}
```

#### Properties

Since XML documentation does not support annotating individual property accessors (`get` or `set`), the analyzer uses heuristics to infer context. Keywords like "gets" and "sets" help determine which accessor an exception applies to.

##### Example: Property Diagnostics

Consider the ``StringBuilder.Length`` property, with this XML documentation:

```xml
/// <summary>
/// Gets or sets the length of the current StringBuilder object.
/// </summary>
/// <value>
/// The number of characters in the current StringBuilder object.
/// </value>
/// <exception cref="ArgumentOutOfRangeException">
/// The value specified for a set operation is less than zero or greater than the current capacity.
/// </exception>
```

The analyzer determines the exception applies to the setter:

```csharp
using System.Text;

StringBuilder stringBuilder = new StringBuilder();

var length = stringBuilder.Length; // No exception

try
{
    stringBuilder.Length = 4; // Throws ArgumentOutOfRangeException
}
catch (ArgumentOutOfRangeException)
{
    // Handle exception
}
```

### Nullable Context

In a nullable context, the analyzer suppresses warnings for exceptions unlikely to occur due to nullability rules, minimizing redundant checks.

#### ArgumentNullException

When parameters are marked as non-nullable, the analyzer ignores `ArgumentNullException` diagnostics, as tooling enforces null safety.

```csharp
#nullable enable

public void TestMethod()
{
    // No diagnostic for ArgumentNullException
    var x = int.Parse("42");
}
```

#### NullReferenceException

`NullReferenceException` is not explicitly declared but occurs at runtime when null values are improperly handled. With nullable contexts and proper null checks, these exceptions are rare.

---

By leveraging nullable contexts, XML documentation, and `ThrowsAttribute`, the analyzer offers a comprehensive solution for exception handling, accommodating both annotated and unannotated libraries while promoting robust code practices.
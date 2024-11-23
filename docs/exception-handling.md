# Exception Handling with the CheckedExceptions Analyzer

### Overview

The CheckedExceptions analyzer enhances exception management in your C# projects by:

1. **Identifying Exception Sources**: Detecting `throw` statements or method calls where exceptions may be thrown or propagated.
2. **Reporting Diagnostics**: Flagging unhandled exceptions, prompting developers to handle them explicitly or declare their propagation.

### Defining the `ThrowsAttribute`

While the analyzer can automatically detect exceptions being thrown, explicitly using the `ThrowsAttribute` adds significant value. It allows you to annotate methods, constructors, or delegates with the exceptions they might throw, enabling the analyzer to generate more precise diagnostics. This is particularly useful in complex methods where automatic detection might not capture all potential exceptions.

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
                throw new ArgumentException("ExceptionType must be an Exception type.", nameof(exceptionType));

            ExceptionType = exceptionType;
        }
    }
}
```

### Handling Exceptions

#### Best Practices

- **Handle Exceptions Locally**: Whenever possible, catch and handle exceptions within the method where they occur.
- **Propagate with Care**: If you must propagate exceptions, use the `ThrowsAttribute` to declare them, but consider it a last resort.
- **Avoid Swallowing Exceptions**: Do not catch exceptions without proper handling, as this can make debugging difficult.

#### Example: Unhandled Exception

A simple `throw` statement without handling generates a diagnostic indicating the exception is unhandled:

```csharp
public void Foo()
{
    throw new InvalidOperationException();
}
```

**Handling the Exception Locally**

To address this, you should catch and handle the exception:

```csharp
public void Foo()
{
    try
    {
        throw new InvalidOperationException();
    }
    catch (InvalidOperationException ex)
    {
        // Handle the exception appropriately
    }
}
```

**Propagating the Exception**

Alternatively, you can declare that the method propagates the exception using `ThrowsAttribute`. This approach should be used judiciously, as it shifts the responsibility of handling the exception to the caller.

```csharp
[Throws(typeof(InvalidOperationException))]
public void Foo()
{
    throw new InvalidOperationException();
}
```

**Handling the Propagated Exception at the Call Site**

The caller can then handle the exception:

```csharp
try
{
    Foo();
}
catch (InvalidOperationException ex)
{
    // Handle the exception appropriately
}
```

The analyzer provides code fixes for handling or propagating exceptions, aiding in maintaining robust code.

#### Example: Multiple Exceptions and Exception Hierarchies

You can annotate methods with multiple `ThrowsAttribute` declarations to indicate all potential exceptions:

```csharp
[Throws(typeof(ArgumentOutOfRangeException))]
[Throws(typeof(InvalidOperationException))]
public void Foo()
{
    // Code that might throw exceptions
}
```

When handling these exceptions, it's important to consider the exception hierarchy. Catching a base exception type will also handle derived exceptions.

```csharp
try
{
    Foo();
}
catch (ArgumentException ex)
{
    // Handles ArgumentException and its derived types, such as ArgumentOutOfRangeException
}
catch (InvalidOperationException ex)
{
    // Handle InvalidOperationException
}
```

**Exception Hierarchy Visualized**

```
Exception
└── SystemException
    └── ArgumentException
        └── ArgumentOutOfRangeException
```

Catching `ArgumentException` will also catch `ArgumentOutOfRangeException` due to inheritance.

### Integrating with Unannotated Libraries

To ensure compatibility with libraries that lack `ThrowsAttribute` annotations, the analyzer uses XML documentation comments to infer potential exceptions. This approach serves as a fallback mechanism but is less reliable than explicit annotations to the code itself.

#### Example: Unannotated Library

Consider a library without `ThrowsAttribute` annotations:

```csharp
#nullable disable

using System;

public class TestClass
{
    private int _value;

    /// <summary>
    /// A property.
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
                throw new ArgumentOutOfRangeException(nameof(value), "Value can't be less than 42");
            }
            _value = value;
        }
    }

    /// <summary>
    /// A method.
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

#### Using the Unannotated Library

Here's how you might use this library in a project with the analyzer installed:

```csharp
#nullable disable

TestClass test = new TestClass();

try
{
    test.Process(null); // May throw ArgumentNullException
}
catch (ArgumentNullException ex)
{
    // Handle ArgumentNullException
}

try
{
    test.Value = 30; // May throw ArgumentOutOfRangeException
}
catch (ArgumentOutOfRangeException ex)
{
    // Handle ArgumentOutOfRangeException
}
```

### Handling Properties

Since XML documentation does not support annotating individual property accessors (`get` or `set`), the analyzer uses heuristics to infer the context of exceptions based on the documentation.

#### Example: Property Diagnostics

Consider the `StringBuilder.Length` property, which has the following XML documentation:

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

The analyzer deduces that the `ArgumentOutOfRangeException` applies to the setter:

```csharp
using System.Text;

StringBuilder stringBuilder = new StringBuilder();

// Getting the Length does not throw an exception
var length = stringBuilder.Length;

try
{
    // Setting Length to a negative value throws ArgumentOutOfRangeException
    stringBuilder.Length = -1;
}
catch (ArgumentOutOfRangeException ex)
{
    // Handle the exception appropriately
}
```

### Nullable Context Interaction

In a nullable context, the analyzer adjusts diagnostics to avoid redundant warnings for exceptions that are unlikely to occur due to nullability annotations.

#### Suppressing `ArgumentNullException` Diagnostics

When parameters are marked as non-nullable, the analyzer suppresses diagnostics for `ArgumentNullException`, as the compiler enforces null safety:

```csharp
#nullable enable

TestClass test = new TestClass();

string? str = "42";

// Compiler warning: Passing nullable parameter 'str' to non-nullable parameter
test.Process(str); // Warning generated by the compiler, not the analyzer
```

Without nullable enabled you would get a diagnostic for `ArgumentNullException`:

```csharp
#nullable disable

TestClass test = new TestClass();

string str = null;

test.Process(str); // Diagnostic generated by the analyzer for potential ArgumentNullException
```

Of course, nullable can be enabled on a project level.

#### Handling `NullReferenceException`

`NullReferenceException` occurs at runtime when null values are improperly handled. They are neither declared or handled by the analyzer.

### Configuration via Settings File

You can customize how exceptions are reported by adding a `CheckedExceptions.settings.json` file to your project. This file allows you to ignore specific exceptions or downgrade them to informational messages.

#### Example Configuration

Create a `CheckedExceptions.settings.json` file with the following content:

```json
{
    "ignoredExceptions": [
        "System.TimeoutException"
    ],
    "informationalExceptions": [
        "System.NotImplementedException",
        "System.IO.IOException"
    ]
}
```

**Note:** Ignoring `System.ArgumentNullException` may not be necessary when nullable annotations are enabled, as the analyzer already handles this scenario.

#### Registering the Settings File

Add the settings file to your `.csproj` file:

```xml
<ItemGroup>
    <AdditionalFiles Include="CheckedExceptions.settings.json" />
</ItemGroup>
```

#### Behavior

- **`ignoredExceptions`**: Exceptions listed here will be completely ignored—no diagnostics or error reports will be generated.
- **`informationalExceptions`**: Exceptions listed here will generate informational diagnostics but won't be reported as errors.

#### Use Cases

- **Silencing Known Exceptions**: Prevent known, non-critical exceptions from cluttering your diagnostics.
- **Non-Disruptive Tracking**: Monitor potential issues by logging them as informational messages without treating them as critical errors.

### Performance Considerations

The analyzer operates during the compilation process and is designed to have minimal impact on build performance. By leveraging existing compiler mechanisms and efficient code analysis techniques, it ensures that your development workflow remains smooth.

### Additional Resources

- [Official C# Exception Handling Documentation](https://docs.microsoft.com/dotnet/csharp/fundamentals/exceptions)
- [Understanding Nullable Reference Types](https://docs.microsoft.com/dotnet/csharp/nullable-references)
- [GitHub Repository for CheckedExceptions Analyzer](https://github.com/YourRepository/CheckedExceptions)

### Contributing and Feedback

We welcome contributions and feedback from the community! If you encounter issues, have suggestions for improvements, or want to contribute code, please visit our [GitHub Issues](https://github.com/YourRepository/CheckedExceptions/issues) page.

---

By leveraging the `ThrowsAttribute`, XML documentation, and nullable contexts, the CheckedExceptions analyzer provides a comprehensive solution for exception handling. It accommodates both annotated and unannotated libraries, promotes best practices, and helps maintain robust, reliable code.
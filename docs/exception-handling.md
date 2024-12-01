# Exception Handling with the CheckedExceptions Analyzer

This document outlines the behavior of the analyzer.

---

## Table of Contents

1. [Overview](#overview)
2. [Understanding Exceptions in .NET](#understanding-exceptions-in-net)
    1. [Overview](#understanding-exceptions-in-net-1)
    2. [How Exceptions Work](#how-exceptions-work)
    3. [Exceptions Are Exceptional](#exceptions-are-exceptional)
    4. [Why This Matters](#why-this-matters)
3. [Defining the `ThrowsAttribute`](#defining-the-throwsattribute)
4. [Handling Exceptions](#handling-exceptions)
    1. [Best Practices](#best-practices)
    2. [Example: Unhandled Exception](#example-unhandled-exception)
        1. [Handling the Exception Locally](#handling-the-exception-locally)
        2. [Propagating the Exception](#propagating-the-exception)
        3. [Handling the Propagated Exception at the Call Site](#handling-the-propagated-exception-at-the-call-site)
    3. [Example: Multiple Exceptions and Exception Hierarchies](#example-multiple-exceptions-and-exception-hierarchies)
        1. [Exception Hierarchy Visualized](#exception-hierarchy-visualized)
5. [Integrating with Unannotated Libraries](#integrating-with-unannotated-libraries)
    1. [Example: Unannotated Library](#example-unannotated-library)
    2. [Using the Unannotated Library](#using-the-unannotated-library)
    3. [Handling Properties in XML documentation](#handling-properties-in-xml-documentation)
        1. [Example: Property Diagnostics](#example-property-diagnostics)
7. [Nullable Context Interaction](#nullable-context-interaction)
    1. [Suppressing `ArgumentNullException` Diagnostics](#suppressing-argumentnullexception-diagnostics)
    2. [Handling `NullReferenceException`](#handling-nullreferenceexception)
8. [Configuration via Settings File](#configuration-via-settings-file)
    1. [Use Cases](#use-cases)
    2. [Example Configuration](#example-configuration)
    3. [Registering the File](#registering-the-file)
    4. [Behavior](#behavior)
    5. [Informational Exceptions Modes](#informational-exceptions-modes)
9. [Performance Considerations](#performance-considerations)
10. [Additional Resources](#additional-resources)
11. [Contributing and Feedback](#contributing-and-feedback)

---

## Overview

The **CheckedExceptions Analyzer** enhances exception management in your C# projects by:

1. **Identifying Exception Sources**: Detecting `throw` statements or method calls where exceptions may be thrown or propagated.
2. **Reporting Diagnostics**: Flagging unhandled exceptions, prompting developers to handle them explicitly or declare their propagation.

---

## Understanding Exceptions in .NET

### Overview

Exceptions are a fundamental part of .NET's runtime error-handling mechanism, enabling programs to react to and recover from runtime errors. Originally inherited from Java (and ultimately influenced by C++), exception handling in .NET works by **unwinding the call stack** to locate an appropriate `catch` block, or terminating the program if none exists.

While exceptions are powerful, they come with trade-offs and best practices that the **CheckedExceptions Analyzer** helps enforce.

### How Exceptions Work

1. **Call Stack Unwinding**:  
   When an exception is thrown, the runtime traverses the call stack to locate a `catch` block capable of handling the exception. If no handler is found, the application terminates. This traversal, known as *stack unwinding*, is computationally expensive and interrupts normal program flow.

2. **Exception Costs**:  
   Throwing and catching exceptions is slower than ordinary method calls and control flow. Overusing exceptions for non-exceptional cases can degrade application performance and readability.

3. **Best Practices for Handling Exceptions**:  
   Handle exceptions **as close as possible** to where they are thrown. This minimizes the scope of the issue and allows more specific, meaningful error handling. The **CheckedExceptions Analyzer** assists by identifying where exceptions are thrown and providing diagnostics for unhandled or mismanaged exceptions.

---

### Exceptions Are Exceptional

Exceptions should signal truly exceptional or error conditions, not serve as a regular control flow mechanism. To adhere to this principle:

1. **Avoid Throwing Exceptions Yourself**:  
   Whenever feasible, use alternative mechanisms, such as result objects, to convey the status of operations.

   ```csharp
   public class Result
   {
       public bool IsSuccess { get; }
       public string ErrorMessage { get; }

       private Result(bool isSuccess, string errorMessage)
       {
           IsSuccess = isSuccess;
           ErrorMessage = errorMessage;
       }

       public static Result Success() => new Result(true, string.Empty);
       public static Result Failure(string errorMessage) => new Result(false, errorMessage);
   }

   public Result ValidateInput(string input)
   {
       if (string.IsNullOrEmpty(input))
           return Result.Failure("Input cannot be null or empty.");
       
       return Result.Success();
   }
   ```

   The analyzer focuses on identifying and encouraging proper exception usage in cases where exceptions are truly necessary.

2. **Be Cautious with Base Class Exceptions**:  
   Avoid throwing or declaring exceptions of base types like `System.Exception` or `System.SystemException`, as they provide limited information about the error. Use specific exception types like `ArgumentNullException` or `InvalidOperationException` for clarity and precision.

3. **Understand Inheritance Hierarchies**:  
   The analyzer is aware of exception inheritance hierarchies. For example, catching `System.Exception` will handle all exceptions, including derived types, potentially masking issues.

   ```csharp
   try
   {
       // Risky code
   }
   catch (Exception ex) // Broad and may unintentionally suppress errors
   {
       // Handle all exceptions, but this is not a recommended practice
   }
   ```

   Instead, catch and handle exceptions based on their specific type:

   ```csharp
   try
   {
       // Risky code
   }
   catch (ArgumentNullException ex) // Handle specific cases
   {
       // Handle ArgumentNullException
   }
   catch (InvalidOperationException ex)
   {
       // Handle InvalidOperationException
   }
   ```

---

### Why This Matters

The **CheckedExceptions Analyzer** reinforces these principles by:

1. **Guiding Exception Placement**:  
   Highlighting unhandled exceptions and providing fixes for proper handling or propagation.

2. **Enforcing Specificity**:  
   Warning against using overly broad exception types and suggesting more precise alternatives.

3. **Encouraging Best Practices**:  
   Promoting alternatives like result objects for expected outcomes to reduce the misuse of exceptions. Detecting when exceptions are handled far from where they are thrown, which can lead to brittle and hard-to-maintain code.

By integrating exception-handling best practices into your workflow, you ensure that exceptions remain a tool for managing truly exceptional conditions, improving the clarity, maintainability, and performance of your applications.

## Defining the `ThrowsAttribute`

Using the `ThrowsAttribute` allows you to annotate methods, constructors, or delegates with the exceptions they might throw, enabling the analyzer to generate more precise diagnostics. This is particularly useful in complex methods where automatic detection might not capture all potential exceptions.

The analyzer only requires the `ThrowsAttribute` to define a constructor with one or more parameters of type ``Type``. It can be single parameters, or a ``params`` array, or combined.

This implementations provides some extra validation logic for the arguments:

```csharp
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Delegate, AllowMultiple = true)]
public class ThrowsAttribute : Attribute
{
    public List<Type> ExceptionTypes { get; } = new List<Type>();

    public ThrowsAttribute(Type exceptionType, params Type[] exceptionTypes)
    {
        if (!typeof(Exception).IsAssignableFrom(exceptionType))
            throw new ArgumentException("Must be an Exception type.");

        ExceptionTypes.Add(exceptionType);

        foreach (var type in exceptionTypes)
        {
            if (!typeof(Exception).IsAssignableFrom(type))
                throw new ArgumentException("Must be an Exception type.");

            ExceptionTypes.Add(type);
        }
    }
}
```

## Handling Exceptions

### Best Practices

- **Handle Exceptions Locally**: Whenever possible, catch and handle exceptions within the method where they occur.
- **Propagate with Care**: If you must propagate exceptions, use the `ThrowsAttribute` to declare them, but consider it a last resort.
- **Avoid Swallowing Exceptions**: Do not catch exceptions without proper handling, as this can make debugging difficult.

### Example: Unhandled Exception

A simple `throw` statement without handling generates a diagnostic indicating the exception is unhandled:

```csharp
public void Foo()
{
    // THROW001: Exception "InvalidOperationException" is thrown or unhandled
    throw new InvalidOperationException();
}
```

#### Handling the Exception Locally

To address this, catch and handle the exception:

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

#### Propagating the Exception

Alternatively, declare that the method propagates the exception using `ThrowsAttribute`. This approach should be used judiciously, as it shifts the responsibility of handling the exception to the caller.

```csharp
[Throws(typeof(InvalidOperationException))]
public void Foo()
{
    throw new InvalidOperationException();
}
```

When unhandled at call site:

```csharp
// THROW001: Exception "InvalidOperationException" might be thrown or unhandled

Foo();
```

#### Handling the Propagated Exception at the Call Site

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

### Example: Multiple Exceptions and Exception Hierarchies

You can annotate methods with multiple `ThrowsAttribute` declarations to indicate all potential exceptions:

```csharp
[Throws(
    typeof(ArgumentOutOfRangeException), 
    typeof(InvalidOperationException))]
public void Foo()
{
    // Code that might throw exceptions
}
```

When unhandled at call site:

```csharp
// THROW001: Exception "ArgumentOutOfRangeException" might be thrown or unhandled
// THROW001: Exception "InvalidOperationException" might be thrown or unhandled

Foo();
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

#### Exception Hierarchy Visualized

```
Exception
└── SystemException
    └── ArgumentException
        └── ArgumentOutOfRangeException
```

Catching `ArgumentException` will also catch `ArgumentOutOfRangeException` due to inheritance.

## Integrating with Unannotated Libraries

To ensure compatibility with libraries that lack `ThrowsAttribute` annotations, the analyzer uses XML documentation comments to infer potential exceptions. This approach serves as a fallback mechanism but is less reliable than explicit annotations in the code itself.

### Example: Unannotated Library

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

### Using the Unannotated Library

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

### Handling Properties in XML documentation

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

## Nullable Context Interaction

In a nullable context, the analyzer adjusts diagnostics to avoid redundant warnings for exceptions that are unlikely to occur due to nullability annotations.

### Suppressing `ArgumentNullException` Diagnostics

When parameters are marked as non-nullable, the analyzer suppresses diagnostics for `ArgumentNullException`, as the compiler enforces null safety:

```csharp
#nullable enable

TestClass test = new TestClass();

string? str = "42";

// Compiler warning: Passing nullable parameter 'str' to non-nullable parameter
test.Process(str); // Warning generated by the compiler, not the analyzer
```

Without nullable enabled, you would get a diagnostic for `ArgumentNullException`:

```csharp
#nullable disable

TestClass test = new TestClass();

string str = null;

test.Process(str); // Diagnostic generated by the analyzer for potential ArgumentNullException
```

**Note:** Nullable can be enabled on a project level.

### Handling `NullReferenceException`

`NullReferenceException` occurs at runtime when null values are improperly handled. They are neither declared nor handled by the analyzer.

## Configuration via Settings File

You can customize how exceptions are reported by adding a `CheckedExceptions.settings.json` file to your project. This file allows you to silence or downgrade specific exceptions to informational messages based on their context.

### Use Cases

- **Silencing Known Exceptions**: Prevent known, non-critical exceptions from cluttering your diagnostics.
- **Non-Disruptive Tracking**: Monitor potential issues by logging them as informational messages without treating them as critical errors.

#### Example Configuration

Create a `CheckedExceptions.settings.json` file with the following structure:

```json
{
    "ignoredExceptions": [
        "System.ArgumentNullException"
    ],
    "informationalExceptions": {
        "System.NotImplementedException": "Throw",
        "System.IO.IOException": "Propagation",
        "System.TimeoutException": "Always"
    }
}
```

There is a JSON schema provided.

**Note:** Ignoring `System.ArgumentNullException` may not be necessary when nullable annotations are enabled, as the analyzer already handles this scenario.
### Registering the File

Add the settings file to your `.csproj`:

```xml
<ItemGroup>
    <AdditionalFiles Include="CheckedExceptions.settings.json" />
</ItemGroup>
```

### Behavior

- **`ignoredExceptions`**: Exceptions listed here will be completely ignored—no diagnostics or error reports will be generated.
- **`informationalExceptions`**: Exceptions listed here will generate informational diagnostics but won't be reported as errors.

### Informational Exceptions Modes

The `informationalExceptions` section allows you to specify the context in which an exception should be treated as informational. The available modes are:

| Mode          | Description                                                                                                       |
|---------------|-------------------------------------------------------------------------------------------------------------------|
| `Throw`       | The exception is considered informational when thrown directly within the method.                                |
| `Propagation` | The exception is considered informational when propagated (re-thrown or passed up the call stack).                |
| `Always`      | The exception is always considered informational, regardless of context.                                         |

**Example Scenario:**

- **`System.IO.IOException`**: When thrown directly (e.g., within a method), it might be critical. However, when propagated from a utility method like `System.Console.WriteLine`, it’s unlikely and can be treated as informational.

## Performance Considerations

The analyzer operates during the compilation process and is designed to have minimal impact on build performance. By leveraging existing compiler mechanisms and efficient code analysis techniques, it ensures that your development workflow remains smooth.

## Additional Resources

- [Official C# Exception Handling Documentation](https://docs.microsoft.com/dotnet/csharp/fundamentals/exceptions)
- [Understanding Nullable Reference Types](https://docs.microsoft.com/dotnet/csharp/nullable-references)
- [GitHub Repository for CheckedExceptions Analyzer](https://github.com/marinasundstrom/CheckedExceptions)

## Contributing and Feedback

We welcome contributions and feedback from the community! If you encounter issues, have suggestions for improvements, or want to contribute code, please visit our [GitHub Issues](https://github.com/marinasundstrom/CheckedExceptions/issues) page.

---

By leveraging the `ThrowsAttribute`, XML documentation, and nullable contexts, the **CheckedExceptions Analyzer** provides a comprehensive solution for exception handling. It accommodates both annotated and unannotated libraries, promotes best practices, and helps maintain robust, reliable code.
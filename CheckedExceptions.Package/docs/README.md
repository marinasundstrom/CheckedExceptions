# Checked Exceptions for C#

**Enforce explicit exception handling in C#/.NET by ensuring all exceptions are either handled or declared.**

This analyzer works with existing class libraries (including .NET class libraries) that have exceptions declared in XML documentation.

[**Repository**](https://github.com/marinasundstrom/CheckedExceptions) • [**NuGet Package**](https://www.nuget.org/packages/Sundstrom.CheckedExceptions)

---

## Table of Contents

1. [Features](#features)
2. [Installation](#installation)
3. [Usage](#usage)
    - [Defining `ThrowsAttribute`](#defining-throwsattribute)
    - [Annotating Methods](#annotating-methods)
    - [Annotating Properties](#annotating-properties)
    - [Handling Exceptions](#handling-exceptions)
    - [Diagnostics](#diagnostics)
5. [Code Fixes](#code-fixes)
    - [Add `ThrowsAttribute`](#add-throwsattribute)
    - [Add `try-catch` Block](#add-try-catch-block)
6. [XML Documentation Support](#xml-documentation-support)
7. [Suppressing Diagnostics](#suppressing-diagnostics)
    - [Using Pragma Directives](#using-pragma-directives)
    - [Suppressing with Attributes](#suppressing-with-attributes)
8. [Configuration](#configuration)
    - [EditorConfig Settings](#editorconfig-settings)
    - [Treating Warnings as Errors](#treating-warnings-as-errors)
9. [Examples](#examples)
    - [Basic Usage](#basic-usage)
    - [Handling Exceptions Example](#handling-exceptions-example)
10. [Contributing](#contributing)
11. [License](#license)

---

## Features

- **Enforce Explicit Exception Handling:** Ensure all exceptions are either caught or explicitly declared using attributes.
- **Seamless Integration:** Works with existing .NET class libraries that include exceptions in their XML documentation.
- **Custom Diagnostics:** Provides clear diagnostics for unhandled or undeclared exceptions.
- **Support for XML Documentation:** Leverages XML docs to identify exceptions from unannotated libraries.
- **Code Fixes:** Offers automated solutions to streamline exception handling and declaration.

## Installation

You can install the package via the [NuGet Gallery](https://www.nuget.org/packages/Sundstrom.CheckedExceptions):

```bash
Install-Package Sundstrom.CheckedExceptions
```

Or via the .NET CLI:

```bash
dotnet add package Sundstrom.CheckedExceptions
```

## Usage

### Defining `ThrowsAttribute`

To utilize **CheckedExceptions**, you need to define the `ThrowsAttribute` in your project. Here's a simple implementation:

```csharp
using System;

namespace CheckedExceptions
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

**Notes:**

- **Namespace Choice:** It's advisable to place `ThrowsAttribute` in a custom namespace (e.g., `CheckedExceptions`) rather than the `System` namespace to avoid potential conflicts with existing .NET types.

### Annotating Methods

Annotate methods, constructors, or delegates that throw exceptions to declare the exceptions they can propagate.

```csharp
using CheckedExceptions;

public class Sample
{
    [Throws(typeof(InvalidOperationException))]
    public void PerformOperation()
    {
        // Some operation that might throw InvalidOperationException
        throw new InvalidOperationException("An error occurred during the operation.");
    }
}
```

### Annotating Properties

You can also apply `ThrowsAttribute` to property getters and setters to declare exceptions they might throw.

```csharp
using CheckedExceptions;

public class DataProcessor
{
    private string? _data;


    public string? Data
    {
        [Throws(typeof(InvalidOperationException))]
        get
        {
            if (_data is null)
                throw new InvalidOperationException("Data is null.");
            return _data;
        }

        [Throws(typeof(ArgumentNullException))]
        set
        {
            if (value is null)
                throw new ArgumentNullException(nameof(value), "Data cannot be null.");
            _data = value;
        }
    }
}
```

### Handling Exceptions

Ensure that any method invoking annotated methods either handles the declared exceptions or further declares them.

```csharp
using System;
using CheckedExceptions;

public class Sample
{
    public void Execute()
    {
        try
        {
            PerformOperation();
        }
        catch (InvalidOperationException ex)
        {
            // Handle invalid operations
        }
    }

    [Throws(typeof(InvalidOperationException))]
    public void PerformOperation()
    {
        // Some operation that might throw InvalidOperationException
        throw new InvalidOperationException("An error occurred during the operation.");
    }
}
```

### Diagnostics

**`THROW001` (Unhandled Exception):** In the `Execute` method, if `PerformOperation` throws an `InvalidOperationException` that is neither caught nor declared using `ThrowsAttribute`, the analyzer will report this diagnostic.

**Example Diagnostic Message:**

```
THROW001: Exception `InvalidOperationException` is thrown by `PerformOperation` but neither caught nor declared via `ThrowsAttribute`.
```

After properly handling the exception, no diagnostics are expected.

## Code Fixes

**CheckedExceptions** provides automated code fixes to simplify exception handling and declaration. These code fixes enhance developer productivity by offering quick solutions to common exception management scenarios.

#### 1. **Add `ThrowsAttribute`**

Automatically adds the `ThrowsAttribute` to methods that propagate exceptions without declarations.

**Example:**

**Before Applying Code Fix:**

```csharp
public class Sample
{
    public void PerformOperation()
    {
        // An exception is thrown but not declared
        throw new InvalidOperationException("An error occurred.");
    }
}
```

**After Applying Code Fix:**

```csharp
using CheckedExceptions;

public class Sample
{
    [Throws(typeof(InvalidOperationException))]
    public void PerformOperation()
    {
        // An exception is thrown and declared
        throw new InvalidOperationException("An error occurred.");
    }
}
```

**How to Apply:**

- **Visual Studio:** Hover over the diagnostic warning, click on the light bulb icon, and select **"Add ThrowsAttribute"** from the suggested fixes.
- **Visual Studio Code:** Similar steps apply using the quick fix light bulb.

#### 2. **Add `try-catch` Block**

Provides an automated way to wrap code that may throw exceptions within a `try-catch` block, ensuring that exceptions are properly handled.

**Example:**

**Before Applying Code Fix:**

```csharp
public class Sample
{
    public void Execute()
    {
        // An exception is thrown but not handled
        PerformOperation();
    }

    [Throws(typeof(InvalidOperationException))]
    public void PerformOperation()
    {
        throw new InvalidOperationException("An error occurred.");
    }
}
```

**After Applying Code Fix:**

```csharp
public class Sample
{
    public void Execute()
    {
        try
        {
            PerformOperation();
        }
        catch (InvalidOperationException ex)
        {
            // Handle the exception
            // TODO: Add exception handling logic here
        }
    }

    [Throws(typeof(InvalidOperationException))]
    public void PerformOperation()
    {
        throw new InvalidOperationException("An error occurred.");
    }
}
```

**How to Apply:**

- **Visual Studio:** Hover over the diagnostic warning, click on the light bulb icon, and select **"Add try-catch block"** from the suggested fixes.
- **Visual Studio Code:** Similar steps apply using the quick fix light bulb.

**Benefits:**

- **Consistency:** Ensures that exception handling follows a standardized pattern across the codebase.
- **Efficiency:** Saves time by automating repetitive coding tasks related to exception management.

**Notes:**

- **Customization:** While the analyzer provides default templates for `try-catch` blocks, you can customize the generated code as needed to fit your project's specific requirements.

## XML Documentation Support

**CheckedExceptions** leverages XML documentation to identify exceptions from methods that do not have `ThrowsAttribute` annotations. This is particularly useful for:

- **Unannotated Libraries:** Works with libraries that lack explicit exception annotations by using their XML documentation.
- **.NET Class Libraries:** Extends support to the .NET framework by reading exceptions documented in XML.

If a library has both XML docs with exceptions and `ThrowsAttribute` annotations, the analyzer combines exceptions from both sources.

**Example:**

```csharp
using System;
using System.IO;

public class FrameworkSample
{
    public void WriteToConsole()
    {
        // THROW001: Exception `IOException` is thrown by `Console.WriteLine` but neither caught nor declared via `ThrowsAttribute`.
        Console.WriteLine("Hello, World!");
    }
}

// Note: The Console class below is a simplified mock for demonstration purposes.
/// <summary>
/// Writes the specified value, followed by the current line terminator, to the standard output stream.
/// </summary>
/// <param name="value">
/// The value to write to the output. Can be of various types (e.g., string, object, etc.).
/// </param>
/// <remarks>
/// This method writes a line of text to the console. It automatically appends a newline at the end of the output.
/// </remarks>
/// <exception cref="System.IO.IOException">
/// An I/O error occurred.
/// </exception>
public class Console
{
    public void WriteLine(string value)
    {
        // Implemented in .NET
    }
}
```

In the above example, the analyzer identifies that `Console.WriteLine` can throw an `IOException` based on the XML documentation, even though `ThrowsAttribute` is not used.

### Annotating Properties

For properties, exceptions are typically documented within the property's XML comments. We employ heuristic conventions to determine which accessor an exception is associated with:

- **Getter Exceptions:** If the XML comments mention phrases like "get", "gets", or "getting", the exception is attributed to the getter.
- **Setter Exceptions:** If the comments include phrases such as "set", "sets", or "setting", the exception is attributed to the setter.

**Example:**

Assuming this is a third-party library that has not been annotated.

```csharp
using CheckedExceptions;

public class DataProcessor
{
    private string? _data;

    /// <summary>
    /// Gets or sets Data
    /// </summary>
    /// </remarks>
    /// <exception cref="System.InvalidOperationException<">
    /// The data retrieved in get operation is null.
    /// </exception>
    /// <exception cref="System.ArgumentNullException">
    /// The value specified in set operation is null.
    /// </exception>
    public string? Data
    {
        // Can work together with:
        //[Throws(typeof(InvalidOperationException))]
        get
        {
            if (value is null)
                throw new InvalidOperationException("Data is null.");
            return _data;
        }

        // Can work together with:
        //[Throws(typeof(ArgumentNullException))]
        set
        {
            if (value is null)
                throw new ArgumentNullException(nameof(value), "Data cannot be null.");
            _data = value;
        }
    }
}
```

**Notes:**

- **Separate Annotations:** You can annotate getters and setters separately if they throw different exceptions, providing granular control over exception declarations.
- **Heuristic Limitations:** While heuristic conventions help in associating exceptions with the correct accessor, ensure that your XML comments are consistent to maintain accuracy.

## Suppressing Diagnostics

While it's recommended to adhere to the analyzer's rules for optimal exception handling, there might be scenarios where you need to suppress specific diagnostics.

### Using Pragma Directives

You can suppress diagnostics directly in your code using `#pragma` directives. This method is useful for suppressing warnings in specific code blocks.

**Example: Suppressing a Diagnostic Around a Method Call**

```csharp
#pragma warning disable THROW001 // Unhandled exception thrown
    PerformOperation();
#pragma warning restore THROW001 // Unhandled exception thrown
```

**Example: Suppressing a Diagnostic for a Specific Throw**

```csharp
#pragma warning disable THROW001 // Unhandled exception thrown
    throw new InvalidOperationException();
#pragma warning restore THROW001 // Unhandled exception thrown
```

### Suppressing with Attributes

Alternatively, you can suppress diagnostics using the `[SuppressMessage]` attribute. This approach is beneficial for suppressing warnings at the method or class level.

**Example: Suppressing a Diagnostic for a Method**

```csharp
using System.Diagnostics.CodeAnalysis;

[SuppressMessage("Usage", "THROW001:Unhandled exception thrown")]
public void MethodWithSuppressedWarning()
{
    // Method implementation
    throw new InvalidOperationException();
}
```

**Caution:** Suppressing diagnostics should be done sparingly and only when you have a justified reason, as it can undermine the benefits of disciplined exception handling.

## Configuration

The **CheckedExceptions** offers various properties to configure its behavior, allowing you to tailor exception handling to your project's specific needs. These properties can be adjusted using an `.editorconfig` file or directly within your project files.

### EditorConfig Settings

Customize the analyzer's behavior using an `.editorconfig` file. This allows you to enable or disable specific diagnostics and adjust their severity levels.

**Example `.editorconfig` Settings:**

```ini
[*.cs]

# Enable or disable specific diagnostics
dotnet_diagnostic.THROW001.severity = warning
dotnet_diagnostic.THROW003.severity = warning
dotnet_diagnostic.THROW004.severity = warning
dotnet_diagnostic.THROW005.severity = warning

# Example of changing the severity of a diagnostic
# dotnet_diagnostic.THROW001.severity = error
```

**Explanation:**

- **`dotnet_diagnostic.<DiagnosticID>.severity`:** Sets the severity level for a specific diagnostic. Possible values are `none`, `silent`, `suggestion`, `warning`, or `error`.

### Treating Warnings as Errors

Treating warnings as errors is an option to enforce stricter exception handling by elevating specific warnings to errors.

**Example: Turning Specific Warnings into Errors**

To treat the `nullable` warnings and the `THROW001` diagnostic as errors, add the following property to your `.csproj` file:

```xml
<PropertyGroup>
  <WarningsAsErrors>nullable,THROW001</WarningsAsErrors>
</PropertyGroup>
```

**Explanation:**

- **`WarningsAsErrors`:** This MSBuild property allows you to specify a comma-separated list of warning codes that should be treated as errors during compilation.

- **`nullable`:** This standard warning pertains to nullable reference type annotations and warnings introduced in C# 8.0 and later.

- **`THROW001`:** This is the diagnostic ID for unhandled exceptions identified by **CheckedExceptions**.

**Notes:**

- **Selective Enforcement:** By specifying only certain warnings in `WarningsAsErrors`, you can enforce stricter rules where necessary while allowing other warnings to remain as warnings.

- **Gradual Adoption:** This approach enables a gradual transition to more disciplined exception handling by focusing on the most critical diagnostics first.

## Examples

To demonstrate how **CheckedExceptions** integrates into your project, here are some practical examples covering basic usage and exception handling.

### Basic Usage

**Annotating a Method:**

```csharp
using CheckedExceptions;

public class Calculator
{
    [Throws(typeof(DivideByZeroException))]
    public int Divide(int numerator, int denominator)
    {
        if (denominator == 0)
            throw new DivideByZeroException("Denominator cannot be zero.");

        return numerator / denominator;
    }
}
```

### Handling Exceptions Example

**Handling Declared Exceptions:**

```csharp
using System;
using CheckedExceptions;

public class CalculatorClient
{
    private Calculator _calculator = new Calculator();

    public void PerformDivision()
    {
        try
        {
            int result = _calculator.Divide(10, 0);
        }
        catch (DivideByZeroException ex)
        {
            Console.WriteLine("Cannot divide by zero.");
        }
    }
}
```

In this example, the `Divide` method declares that it can throw a `DivideByZeroException` using `ThrowsAttribute`. The `PerformDivision` method handles this exception, thus complying with the analyzer's requirements.

## Contributing

Contributions are welcome! Please follow these steps:

1. Fork the [repository](https://github.com/marinasundstrom/CheckedExceptions).
2. Create a new branch (`git checkout -b feature/YourFeature`).
3. Commit your changes (`git commit -m 'Add some feature'`).
4. Push to the branch (`git push origin feature/YourFeature`).
5. Open a pull request.

Please ensure your code adheres to the project's coding standards and includes appropriate tests.

## License

This project is licensed under the [MIT License](https://github.com/marinasundstrom/CheckedExceptions/blob/main/LICENSE).
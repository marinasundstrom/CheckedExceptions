# Checked Exceptions for C#

**Enforce explicit exception handling in C#/.NET by ensuring all exceptions are either handled or declared.**

This analyzer works with existing class libraries (including .NET class libraries) that have exceptions declared in XML documentation.

[**Repository**](https://github.com/marinasundstrom/CheckedExceptionsAnalyzer) • [**NuGet Package**](#)

---

## Table of Contents

1. [Features](#features)
2. [Installation](#installation)
3. [Usage](#usage)
    - [Defining `ThrowsAttribute`](#defining-throwsattribute)
    - [Annotating Methods](#annotating-methods)
    - [Handling Exceptions](#handling-exceptions)
    - [Diagnostics](#diagnostics)
4. [XML Documentation Support](#xml-documentation-support)
5. [Examples](#examples)
    - [Basic Usage](#basic-usage)
    - [Handling Exceptions](#handling-exceptions-example)
6. [Contributing](#contributing)
7. [License](#license)

---

## Features

- **Enforce Explicit Exception Handling:** Ensure all exceptions are either caught or explicitly declared using attributes.
- **Seamless Integration:** Works with existing .NET class libraries that include exceptions in their XML documentation.
- **Custom Diagnostics:** Provides clear diagnostics for unhandled or undeclared exceptions.
- **Support for XML Documentation:** Leverages XML docs to identify exceptions from unannotated libraries.

## Installation

You can install the package via the [NuGet Gallery](https://www.nuget.org/packages/CheckedExceptionsAnalyzer):

```bash
Install-Package CheckedExceptionsAnalyzer
```

Or via the .NET CLI:

```bash
dotnet add package CheckedExceptionsAnalyzer
```

## Usage

### Defining `ThrowsAttribute`

To utilize **CheckedExceptionsAnalyzer**, you need to define the `ThrowsAttribute` in your project. Here's a simple implementation:

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

## XML Documentation Support

**CheckedExceptionsAnalyzer** leverages XML documentation to identify exceptions from methods that do not have `ThrowsAttribute` annotations. This is particularly useful for:

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

## Examples

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

1. Fork the [repository](https://github.com/marinasundstrom/CheckedExceptionsAnalyzer).
2. Create a new branch (`git checkout -b feature/YourFeature`).
3. Commit your changes (`git commit -m 'Add some feature'`).
4. Push to the branch (`git push origin feature/YourFeature`).
5. Open a pull request.

Please ensure your code adheres to the project's coding standards and includes appropriate tests.

## License

This project is licensed under the [MIT License](LICENSE).
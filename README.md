# CheckedExceptionsAnalyzer

<!-- a ![Build](https://img.shields.io/badge/build-passing-brightgreen) -->
![License](https://img.shields.io/badge/license-MIT-blue.svg)

## Table of Contents

- [Overview](#overview)
- [Purpose](#purpose)
- [Features](#features)
- [Prerequisites](#prerequisites)
- [Installation](#installation)
- [Usage](#usage)
- [Configuration](#configuration)
- [Diagnostic Codes Overview](#diagnostic-codes-overview)
- [Example](#example)
  - [Sample Code](#sample-code)
  - [Diagnostics](#diagnostics)
  - [Handling the Exception](#handling-the-exception)
- [Defining ThrowsAttribute](#defining-throwsattribute)
- [Suppressing Diagnostics](#suppressing-diagnostics)
- [Contributing](#contributing)
- [License](#license)
- [Contact](#contact)

## Overview

**CheckedExceptionsAnalyzer** is a **Roslyn Diagnostic Analyzer** designed for C# projects to enforce robust exception handling practices. It ensures that exceptions thrown within your code are either **properly handled** (caught) or **explicitly declared** using the custom `ThrowsAttribute`. By promoting disciplined exception management, it enhances code reliability, maintainability, and clarity regarding the exceptions that methods and functions may produce.

## Purpose

**CheckedExceptionsAnalyzer** brings the concept of checked exceptions from Java to the .NET ecosystem. It aims to make exception handling explicit and enforceable in C#/.NET by leveraging the compiler to guide developers in catching or declaring exceptions. This approach helps prevent unanticipated runtime errors and promotes better code quality and reliability.

Unlike Java's checked exceptions, **CheckedExceptionsAnalyzer** operates on an opt-in basis and treats exceptions as warnings by default. This design choice ensures that developers can gradually adopt checked exception practices without overwhelming their existing codebases.

## Features

- **Detection of Unhandled Exceptions (`THROW001`):**
  - Identifies exceptions that are thrown or propagated but neither caught within the method nor declared via `ThrowsAttribute`.

- **Avoidance of General Exceptions (`THROW003` & `THROW004`):**
  - Flags the use of general `Exception` types in throws and declarations, encouraging the use of more specific exception types.

- **Prevention of Duplicate Throws Attributes (`THROW005`):**
  - Detects multiple `ThrowsAttribute` declarations for the same exception type within a method or function to eliminate redundancy.

- **Comprehensive Analysis:**
  - Analyzes various C# constructs including methods, constructors, lambda expressions, local functions, property accessors, event assignments, and more to ensure consistent exception handling.

- **XML Documentation Support:**
  - Recognizes and respects `<exception>` elements in XML documentation comments, allowing seamless integration with documented exceptions.

- **Propagation and Inheritance Handling:**
  - Supports propagation of warnings and correctly handles inheritance hierarchies for exceptions, ensuring that derived exceptions are appropriately managed.

- **Code Fixes:**
  - **Add ThrowsAttribute:** Automatically adds `ThrowsAttribute` for propagated exceptions.
  - **Add try-catch Block:** Provides quick fixes to add necessary try-catch blocks for handling exceptions.

## Prerequisites

- [.NET SDK 9.0](https://dotnet.microsoft.com/download) or later
- Supported IDEs: Visual Studio 2022 or later, Visual Studio Code with C# extension

## Installation

### Manual Installation

Alternatively, you can include the analyzer directly in your project by adding a project reference or including the analyzer assembly.

## Usage

Once installed, **CheckedExceptionsAnalyzer** automatically analyzes your C# code during compilation and provides real-time feedback within your development environment (e.g., Visual Studio).

### Exception Handling Enforcement

- **Handled Exceptions:** Ensure that all thrown exceptions are either caught within the method or declared using `ThrowsAttribute`.

- **Declared Exceptions:** Apply the `ThrowsAttribute` to methods, constructors, lambdas, or local functions to declare the exceptions they may throw.

- **Avoid General Exceptions:** Use specific exception types instead of the general `System.Exception` to improve clarity and error handling precision.

### Opt-In Mechanism

Since **CheckedExceptionsAnalyzer** is opt-in, you can selectively enable or disable exception checking for specific methods or members by applying or omitting the `ThrowsAttribute`. This flexibility allows gradual adoption without disrupting existing codebases.

## Configuration

No additional configuration is required to use **CheckedExceptionsAnalyzer**. However, to customize its behavior or integrate with your project's coding standards, you can adjust analyzer settings via [EditorConfig](https://editorconfig.org/).

### Example `.editorconfig` Settings

```ini
# Enable or disable specific diagnostics
dotnet_diagnostic.THROW001.severity = warning
dotnet_diagnostic.THROW003.severity = warning
dotnet_diagnostic.THROW004.severity = warning
dotnet_diagnostic.THROW005.severity = warning

# Configure whether to treat missing ThrowsAttribute as an error
dotnet_diagnostic.THROW001.severity = warning
```

## Diagnostic Codes Overview

| Diagnostic ID | Description |
|---------------|-------------|
| `THROW001`    | **Unhandled exception thrown:** Identifies exceptions that are thrown but neither caught within the method nor declared via `ThrowsAttribute`. |
| `THROW003`    | **General `ThrowsAttribute` usage:** Flags the use of general `Exception` types in `ThrowsAttribute`, encouraging more specific exception declarations. |
| `THROW004`    | **General exception thrown:** Warns against throwing the general `System.Exception` type directly in the code. |
| `THROW005`    | **Duplicate `ThrowsAttribute`:** Detects multiple `ThrowsAttribute` declarations for the same exception type within a method or function. |

## Example

### Sample Code

Annotate a method or other member that throws exceptions. This indicates that they are supposed to propagate to the caller.

```csharp
using System;
using System.IO;

public class Sample
{
    public void Execute()
    {
        // THROW001: Exception `InvalidOperationException` is thrown but neither caught nor declared via `ThrowsAttribute`.
        PerformOperation();
    }

    [Throws(typeof(InvalidOperationException))]
    public void PerformOperation()
    {
        // Some operation that might throw InvalidOperationException
        throw new InvalidOperationException("An error occurred during the operation.");
    }
}
```

**Diagnostic Reported:**

- **`THROW001`:** Exception `InvalidOperationException` is thrown but neither caught nor declared via `ThrowsAttribute`.

### Diagnostics

- **`THROW001` (Unhandled Exception):** In the `Execute` method, `InvalidOperationException` is thrown by `PerformOperation` but is neither caught within `Execute` nor declared using `ThrowsAttribute`.

### Handling the Exception

Handle the exception with a catch statement:

```csharp
using System;
using System.IO;

public class Sample
{
    public void Execute()
    {
        try
        {
            // Exception is properly handled
            PerformOperation();
        }
        catch (InvalidOperationException ex)
        {
            // Handle invalid operations
            Console.WriteLine(ex.Message);
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

### Diagnostics After Handling

- **No Diagnostics Expected:** In the above code, `InvalidOperationException` thrown in `PerformOperation` is declared via `ThrowsAttribute` and is appropriately handled in the `Execute` method. Therefore, no diagnostics are reported.

## Defining ThrowsAttribute

To utilize **CheckedExceptionsAnalyzer**, you need to define the `ThrowsAttribute` in your project. Here's a simple implementation:

```csharp
namespace System;

using System;

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
```

### Usage Example

```csharp
[Throws(typeof(InvalidOperationException))]
public void RiskyMethod()
{
    // Method implementation
    throw new InvalidOperationException("Something went wrong.");
}
```

## Suppressing Diagnostics

While it's recommended to adhere to the analyzer's rules for optimal exception handling, there might be scenarios where you need to suppress specific diagnostics.

### Using Pragma Directives

```csharp
#pragma warning disable THROW001 // Unhandled exception thrown
    // Suppressed handling
    PerformOperation();
#pragma warning restore THROW001 // Unhandled exception thrown
```

Or suppressing a specific throw:

```csharp
#pragma warning disable THROW001 // Unhandled exception thrown
    throw new InvalidOperationException();
#pragma warning restore THROW001 // Unhandled exception thrown
```

### Suppressing in Code

You can also suppress diagnostics using the `[SuppressMessage]` attribute:

```csharp
[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "THROW001:Unhandled exception thrown")]
public void MethodWithSuppressedWarning()
{
    // Method implementation
    throw new InvalidOperationException();
}
```

**Caution:** Suppressing diagnostics should be done sparingly and only when you have a justified reason, as it can undermine the benefits of disciplined exception handling.

## Contributing

Contributions are welcome! If you'd like to contribute to **CheckedExceptionsAnalyzer**, please follow these steps:

1. **Fork the Repository:** Click the "Fork" button on the repository page.

2. **Clone Your Fork:**

   ```bash
   git clone https://github.com/marinasundstrom/CheckedExceptionsAnalyzer.git
   ```

3. **Create a New Branch:**

   ```bash
   git checkout -b feature/YourFeatureName
   ```

4. **Make Your Changes:** Implement your feature or fix.

5. **Commit Your Changes:**

   ```bash
   git commit -m "Add feature X"
   ```

6. **Push to Your Fork:**

   ```bash
   git push origin feature/YourFeatureName
   ```

7. **Open a Pull Request:** Navigate to the original repository and open a pull request detailing your changes.

### Guidelines

- **Code Quality:** Ensure your code adheres to the project's coding standards and passes all existing tests.

- **Documentation:** Update or add documentation as necessary to reflect your changes.

- **Testing:** Include unit tests for new features or bug fixes to maintain the analyzer's reliability.

## License

This project is licensed under the [MIT License](LICENSE).

## Contact

For questions, suggestions, or support, please open an [issue](https://github.com/marinasundstrom/CheckedExceptionsAnalyzer/issues) on the GitHub repository.

---

*This README provides a comprehensive overview and guide for the **CheckedExceptionsAnalyzer** project. For more detailed information or assistance, please refer to the project's documentation or contact the maintainers directly.*
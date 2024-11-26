# Checked Exceptions for C#

**Enforce explicit exception handling in C#/.NET by ensuring all exceptions are either handled or declared.**

<!-- ![Deploy](https://github.com/marinasundstrom/CheckedExceptions/actions/workflows/deploy.yml/badge.svg) -->
![Build](https://github.com/marinasundstrom/CheckedExceptions/actions/workflows/ci.yml/badge.svg)
[![NuGet](https://img.shields.io/nuget/v/Sundstrom.CheckedExceptions.svg)](https://www.nuget.org/packages/Sundstrom.CheckedExceptions/)
![License](https://img.shields.io/badge/license-MIT-blue.svg)

## Table of Contents

- [Overview](#overview)
- [Purpose](#purpose)
- [Features](#features)
- [Prerequisites](#prerequisites)
- [Installation](#installation)
- [Usage](#usage)
- [Configuration](#configuration)
  - [Treating Warnings as Errors](#treating-warnings-as-errors)
  - [Configuration via Settings File](#configuration-via-settings-file)
- [Diagnostic Codes Overview](#diagnostic-codes-overview)
- [Example](#example)
  - [Sample Code](#sample-code)
  - [Diagnostics](#diagnostics)
  - [Handling the Exception](#handling-the-exception)
  - [XML Documentation Support](#xml-documentation-support)
  - [Nullable Context Interaction](#nullable-context-interaction)
- [Defining ThrowsAttribute](#defining-throwsattribute)
- [Suppressing Diagnostics](#suppressing-diagnostics)
- [Contributing](#contributing)
- [License](#license)
- [Contact](#contact)

## Overview

**CheckedExceptions** is a **Roslyn Diagnostic Analyzer** tailored for C# projects to enforce robust and explicit exception handling practices. By ensuring that every exception thrown within your code is either **properly handled** (caught) or **explicitly declared** using the custom `ThrowsAttribute`, this analyzer promotes cleaner, more maintainable, and reliable codebases.

Understanding and managing exceptions effectively is crucial for building resilient applications. **CheckedExceptions** brings the benefits of checked exceptions, familiar from Java, into the .NET ecosystem, encouraging developers to think critically about how exceptions are propagated and handled throughout their applications.

Additionally, methods from the .NET framework, such as `Console.WriteLine`, may throw exceptions like `IOException`. Since these framework methods do not include `ThrowsAttribute` annotations, **CheckedExceptions** leverages XML documentation comments to identify and manage these exceptions effectively.

## Purpose

**CheckedExceptions** aims to make exception handling in C# explicit and enforceable, much like Java's checked exceptions. By declaring exceptions using `ThrowsAttribute`, developers signal which exceptions can propagate from their methods, compelling callers to handle or further declare these exceptions. This explicit declaration helps prevent unanticipated runtime errors and fosters a disciplined approach to error management.

Key objectives include:

- **Explicit Propagation:** Declaring that a method throws an exception with `ThrowsAttribute` means the exception will propagate, requiring the caller to handle it.
  
- **Localized Handling:** Encouraging developers to handle exceptions as close to their source as possible, reducing unnecessary propagation and avoiding complex try-catch hierarchies.
  
- **Thoughtful Design:** The analyzer prompts developers to carefully consider their exception handling strategies, balancing between catching exceptions locally and declaring them for higher-level handling.

Unlike Java's checked exceptions, **CheckedExceptions** is **opt-in** and treats exceptions as **warnings by default**, allowing developers to gradually integrate checked exception practices without overwhelming their existing codebases.

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
  
- **Framework Support:**
  - While the analyzer can process custom frameworks annotated with `ThrowsAttribute` for seamless integration and comprehensive exception handling across the codebase, it also supports handling exceptions from unannotated libraries (such as the .NET framework) by leveraging XML documentation comments.

## Prerequisites

The **CheckedExceptions** targets .NET Standard 2.0 (`netstandard2.0`) to allow it to be used across multiple versions of .NET.

If you want to build this solution, other projects, such as unit tests, require the .NET 9 SDK.

- [.NET SDK 9.0](https://dotnet.microsoft.com/download) or later
- Supported IDEs: Visual Studio 2022 or later, Visual Studio Code with C# extension

## Installation

You can integrate **CheckedExceptions** into your project via [NuGet](https://www.nuget.org/).

### Using .NET CLI

```bash
dotnet add package Sundstrom.CheckedExceptions
```

### Using Package Manager

```powershell
Install-Package Sundstrom.CheckedExceptions
```

### Manual Installation

Alternatively, you can include the analyzer directly in your project by adding a project reference or including the analyzer assembly.

## Usage

Once installed, **CheckedExceptions** automatically analyzes your C# code during compilation and provides real-time feedback within your development environment (e.g., Visual Studio).

### Exception Handling Enforcement

- **Handled Exceptions:** Ensure that all thrown exceptions are either caught within the method or declared using `ThrowsAttribute`.
  
- **Declared Exceptions:** Apply the `ThrowsAttribute` to methods, constructors, property accessors, lambdas, or local functions to declare the exceptions they may throw.
  
- **Avoid General Exceptions:** Use specific exception types instead of the general `System.Exception` to improve clarity and error handling precision.

### Opt-In Mechanism

Since **CheckedExceptions** is opt-in, you can selectively enable or disable exception checking for specific methods or members by applying or omitting the `ThrowsAttribute`. This flexibility allows gradual adoption without disrupting existing codebases.

## Configuration

No additional configuration is required to use **CheckedExceptions**. However, to customize its behavior or integrate with your project's coding standards, you can adjust analyzer settings via [EditorConfig](https://editorconfig.org/).

### Example `.editorconfig` Settings

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

### Treating Warnings as Errors

To enforce stricter exception handling by elevating specific warnings to errors, you can use the `<WarningsAsErrors>` property in your project file. This is particularly useful for maintaining high code quality and ensuring that critical exception handling issues are addressed promptly.

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

**Result:**

With the above configuration:

- Any warnings related to nullable reference types (`nullable`) will be treated as errors, preventing the build from succeeding if such issues are present.
  
- The `THROW001` warnings, which indicate unhandled exceptions that are neither caught nor declared, will also be treated as errors, enforcing stricter exception handling practices.

**Full `.csproj` Example:**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
    <WarningsAsErrors>nullable,THROW001</WarningsAsErrors>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Sundstrom.CheckedExceptions" Version="1.1.9" />
  </ItemGroup>

</Project>
```

**Notes:**

- **Selective Enforcement:** By specifying only certain warnings in `WarningsAsErrors`, you can enforce stricter rules where necessary while allowing other warnings to remain as warnings.
  
- **Gradual Adoption:** This approach enables a gradual transition to more disciplined exception handling by focusing on the most critical diagnostics first.

### Configuration via Settings File

You can further customize how **CheckedExceptions** reports exceptions by adding a `CheckedExceptions.settings.json` file to your project. This settings file allows you to silence specific exceptions or downgrade their severity based on their context, providing greater control over diagnostics.

#### Use Cases

- **Silencing Known Exceptions:** Prevent non-critical, known exceptions from cluttering your diagnostics.
- **Non-Disruptive Tracking:** Monitor potential issues by logging them as informational messages without treating them as critical errors.

#### Example Configuration

Create a `CheckedExceptions.settings.json` file in the root of your project with the following structure:

```json
{
    "ignoredExceptions": [
        "System.ArgumentNullException"
    ],
    "informationalExceptions": {
        "System.NotImplementedException": "Propagation",
        "System.IO.IOException": "Propagation",
        "System.TimeoutException": "Always"
    }
}
```

#### Registering the Settings File

To ensure that **CheckedExceptions** recognizes and applies your custom settings, include the settings file in your project by adding the following to your `.csproj` file:

```xml
<ItemGroup>
    <AdditionalFiles Include="CheckedExceptions.settings.json" />
</ItemGroup>
```

#### Configuration Options

- **`ignoredExceptions`:** 
  - **Description:** Exceptions listed here will be completely ignored—no diagnostics or error reports will be generated.
  - **Usage Example:**
    ```json
    "ignoredExceptions": [
        "System.ArgumentNullException"
    ]
    ```

- **`informationalExceptions`:**
  - **Description:** Exceptions listed here will generate informational diagnostics but won't be reported as errors.
  - **Modes:**
    | Mode          | Description                                                                                   |
    |---------------|-----------------------------------------------------------------------------------------------|
    | `Throw`       | The exception is considered informational when thrown directly within the method.            |
    | `Propagation` | The exception is considered informational when propagated (re-thrown or passed up the call stack). |
    | `Always`      | The exception is always considered informational, regardless of context.                     |
  - **Usage Example:**
    ```json
    "informationalExceptions": {
        "System.NotImplementedException": "Propagation",
        "System.IO.IOException": "Propagation",
        "System.TimeoutException": "Always"
    }
    ```

#### Behavior Explanation

- **`ignoredExceptions`:** Exceptions specified here will be excluded from all diagnostics, effectively silencing any warnings or errors related to them.
  
- **`informationalExceptions`:** 
  - **`Throw` Mode:** Marks exceptions as informational when they are thrown directly within the method, allowing you to monitor their usage without enforcing strict handling.
  - **`Propagation` Mode:** Treats exceptions as informational when they are propagated up the call stack, providing insights into their flow without enforcing handling.
  - **`Always` Mode:** Applies the informational status to exceptions in all contexts, ensuring they are treated as non-critical regardless of where they occur.

#### Example Scenario

Consider the following configuration:

```json
{
    "ignoredExceptions": [
        "System.ArgumentNullException"
    ],
    "informationalExceptions": {
        "System.IO.IOException": "Propagation"
    }
}
```

- **`System.ArgumentNullException`** will be completely ignored by **CheckedExceptions**, meaning no diagnostics will be reported if this exception is or might be thrown or propagated.
- **`System.IO.IOException`** will be treated as informational when it is propagated, allowing you to track its flow without enforcing handling or declaration.

#### Benefits

- **Customized Diagnostics:** Tailor the analyzer's behavior to fit your project's specific needs and coding standards.
- **Reduced Noise:** By silencing or downgrading non-critical exceptions, focus on the most impactful diagnostics.
- **Flexible Enforcement:** Gradually adopt stricter exception handling practices by selectively enabling diagnostics for different exception types.

## Diagnostic Codes Overview

| Diagnostic ID | Description |
|---------------|-------------|
| `THROW001`    | **Unhandled exception:** Identifies exceptions that are directly thrown or might be thrown by member, but neither caught within the context nor declared via `ThrowsAttribute`. |
| `THROW002`    | Informational: Ignored exception may cause runtime issues |
| `THROW003`    | **General `ThrowsAttribute` usage:** Flags the use of general `Exception` types in `ThrowsAttribute`, encouraging more specific exception declarations. |
| `THROW004`    | **General exception thrown:** Warns against throwing the general `System.Exception` type directly in the code. |
| `THROW005`    | **Duplicate `ThrowsAttribute`:** Detects multiple `ThrowsAttribute` declarations for the same exception type within a method or function. |

## Example

### Sample Code

Annotate a method or other member that throws exceptions. This indicates that they are supposed to propagate to the caller.

```csharp
using System;

public class Sample
{
    public void Execute()
    {
        // THROW001: Exception `InvalidOperationException` might be thrown but not handled.
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

### Diagnostics

- **`THROW001` (Unhandled Exception):** In the `Execute` method, `InvalidOperationException` might be thrown by `PerformOperation` but is neither caught within `Execute` nor declared using `ThrowsAttribute`.

### Handling the Exception

Handle the exception with a catch statement:

```csharp
using System;

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

### Multiple ThrowsAttributes Example

A method throwing two exceptions:

```csharp
using System;

public class DataFetcher
{
    [Throws(typeof(NullReferenceException))]
    [Throws(typeof(ArgumentException))]
    public void FetchData(string param)
    {
        if(param == "foo") 
        {
            throw new ArgumentException("Invalid argument provided.", nameof(param));
        }

        throw new NullReferenceException("Data source is null.");
    }
}
```

When you don't handle all exceptions:

```csharp
var fetcher = new DataFetcher();

// THROW001: Exception `NullReferenceException` might be thrown by `FetchData` but not handled
// THROW001: Exception `ArgumentException` might be thrown by `FetchData` but not handled
fetcher.FetchData("test");
```

**Diagnostic Reported:**

- **`THROW001`:** Exception `NullReferenceException` might be thrown but not handled
- **`THROW001`:** Exception `ArgumentException` might be thrown but not handled

**Note:** Ensure that all declared exceptions are actually thrown within the method to prevent unnecessary diagnostics.

Handling all exceptions:

```csharp
using System;

var fetcher = new DataFetcher();

try
{
    fetcher.FetchData("test");
}
catch (NullReferenceException ex)
{
    Console.WriteLine("Handled exception: " + ex.Message);
}
catch (ArgumentException ex)
{
    Console.WriteLine("Handled exception: " + ex.Message);
}
```

Or catch a base class covering all exceptions, such as `Exception`:

```csharp
using System;

try 
{
    var fetcher = new DataFetcher();

    try
    {
        fetcher.FetchData("test");
    }
    catch (Exception ex)
    {
        // This will handle all exceptions declared in ThrowsAttribute
        Console.WriteLine("Handled exception: " + ex.Message);
    }
}
```

Methods from the .NET class library, like `Console.WriteLine`, can throw exceptions such as `IOException`.

### XML Documentation Support

There are many unannotated libraries, and that extends to the .NET class library. Since these framework methods do not include `ThrowsAttribute` annotations, **CheckedExceptions** relies on XML documentation to identify and manage these exceptions.

If a library has both XML docs with exceptions and `ThrowsAttribute` annotations, the exceptions from both will be combined.

Just like any other method, they warn like this:

```csharp
using System;
using System.IO;

public class FrameworkSample
{
    public void WriteToConsole()
    {
        // THROW001: Exception `IOException` might be thrown by `Console.WriteLine` but not handled.
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

## Defining ThrowsAttribute

To utilize **CheckedExceptions**, you need to define the `ThrowsAttribute` in your project. Here's a simple implementation:

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

**Notes:**

- **Namespace Choice:** It's advisable to place `ThrowsAttribute` in a custom namespace (e.g., `CheckedExceptions`) rather than the `System` namespace to avoid potential conflicts with existing .NET types.

### Usage Example

```csharp
using System;

public class Example
{
    [Throws(typeof(InvalidOperationException))]
    public void RiskyMethod()
    {
        // Method implementation
        throw new InvalidOperationException("Something went wrong.");
    }
}
```

## Suppressing Diagnostics

While it's recommended to adhere to the analyzer's rules for optimal exception handling, there might be scenarios where you need to suppress specific diagnostics.

### Using Pragma Directives

```csharp
#pragma warning disable THROW001 // Unhandled exception
    // Suppressed handling
    PerformOperation();
#pragma warning restore THROW001 // Unhandled exception
```

Or suppressing a specific throw:

```csharp
#pragma warning disable THROW001 // Unhandled exception
    throw new InvalidOperationException();
#pragma warning restore THROW001 // Unhandled exception
```

### Suppressing in Code

You can also suppress diagnostics using the `[SuppressMessage]` attribute:

```csharp
using System.Diagnostics.CodeAnalysis;

[SuppressMessage("Usage", "THROW001:Unhandled exception")]
public void MethodWithSuppressedWarning()
{
    // Method implementation
    throw new InvalidOperationException();
}
```

**Caution:** Suppressing diagnostics should be done sparingly and only when you have a justified reason, as it can undermine the benefits of disciplined exception handling.

## Contributing

Contributions are welcome! If you'd like to contribute to **CheckedExceptions**, please follow these steps:

1. **Fork the Repository:** Click the "Fork" button on the repository page.
   
2. **Clone Your Fork:**

   ```bash
   git clone https://github.com/marinasundstrom/CheckedExceptions.git
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

For questions, suggestions, or support, please open an [issue](https://github.com/marinasundstrom/CheckedExceptions/issues) on the GitHub repository.
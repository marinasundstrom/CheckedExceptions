# Checked Exceptions for C#

**Enforce explicit exception handling in C#/.NET by ensuring all exceptions are either handled or declared.**

This analyzer works seamlessly with existing class libraries (including .NET class libraries) that have exceptions declared in XML documentation.

[![GitHub](https://img.shields.io/badge/GitHub-Repository-lightgrey.svg)](https://github.com/marinasundstrom/CheckedExceptions)
![License](https://img.shields.io/github/license/marinasundstrom/CheckedExceptions)

---

## Table of Contents

- [Features](#features)
- [Installation](#installation)
- [Usage](#usage)
  - [Defining `ThrowsAttribute`](#defining-throwsattribute)
  - [Annotating Methods](#annotating-methods)
  - [Annotating Properties](#annotating-properties)
  - [Handling Exceptions](#handling-exceptions)
  - [Diagnostics](#diagnostics)
- [Code Fixes](#code-fixes)
  - [Add `ThrowsAttribute`](#add-throwsattribute)
  - [Add `try-catch` Block](#add-try-catch-block)
- [XML Documentation Support](#xml-documentation-support)
  - [Annotating Properties](#annotating-properties-xml)
- [Suppressing Diagnostics](#suppressing-diagnostics)
  - [Using Pragma Directives](#using-pragma-directives)
  - [Suppressing with Attributes](#suppressing-with-attributes)
- [Configuration](#configuration)
  - [EditorConfig Settings](#editorconfig-settings)
  - [Treating Warnings as Errors](#treating-warnings-as-errors)
  - [Configuration via Settings File](#configuration-via-settings-file)
- [Examples](#examples)
  - [Basic Usage](#basic-usage)
  - [Handling Exceptions Example](#handling-exceptions-example)
- [Contributing](#contributing)
- [Acknowledgements](#acknowledgements)
- [License](#license)

---

## Features

- **Enforce Explicit Exception Handling:** Ensure all exceptions are either caught or explicitly declared using attributes.
- **Seamless Integration:** Works with existing .NET class libraries that include exceptions in their XML documentation.
- **Custom Diagnostics:** Provides clear diagnostics for unhandled or undeclared exceptions.
- **Support for XML Documentation:** Leverages XML docs to identify exceptions from unannotated libraries.
- **Code Fixes:** Offers automated solutions to streamline exception handling and declaration.

---

## Installation

You can install the package via the [NuGet Gallery](https://www.nuget.org/packages/Sundstrom.CheckedExceptions):

```bash
Install-Package Sundstrom.CheckedExceptions
```

Or via the .NET CLI:

```bash
dotnet add package Sundstrom.CheckedExceptions
```

---

## Usage

### Defining `ThrowsAttribute`

To utilize **CheckedExceptions**, define the `ThrowsAttribute` in your project. Here's a simple implementation:

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

**Notes:**

- **Namespace Choice:** It's advisable to place `ThrowsAttribute` in a custom namespace (e.g., `Sundstrom.CheckedExceptions`) rather than the `System` namespace to avoid potential conflicts with existing .NET types.

#### Usage Example

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

Multiple declarations:

```csharp
using System;

public class Example
{
    [Throws(
        typeof(InvalidOperationException),
        typeof(ArgumentException))]
    public void RiskyMethod()
    {
        // Omitted: Code that might throw
    }
}
```

If you prefer, you can have one exception type per `ThrowsAttribute` :

```csharp
using System;

public class Example
{
    [Throws(typeof(InvalidOperationException))]
    [Throws(typeof(ArgumentException))]
    public void RiskyMethod()
    {
        // Omitted: Code that might throw
    }
}
```

### Annotating Methods

Annotate methods, constructors, or delegates that throw exceptions to declare the exceptions they can propagate.

```csharp
using Sundstrom.CheckedExceptions;
using System;

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
using Sundstrom.CheckedExceptions;
using System;

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
using Sundstrom.CheckedExceptions;
using System;

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
            Console.WriteLine($"Handled exception: {ex.Message}");
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

**`THROW001` (Unhandled Exception):** In the `Execute` method, if `PerformOperation` might throw an `InvalidOperationException` that is neither caught nor declared using `ThrowsAttribute`, the analyzer will report this diagnostic.

**Example Diagnostic Message:**

```
THROW001: Exception `InvalidOperationException` is thrown but not handled

After properly handling the exception, no diagnostics are expected.

---

## Code Fixes

**CheckedExceptions** provides automated code fixes to simplify exception handling and declaration. These code fixes enhance developer productivity by offering quick solutions to common exception management scenarios.

### 1. **Add `ThrowsAttribute`**

Automatically adds the `ThrowsAttribute` to methods that propagate exceptions without declarations.

**Example:**

**Before Applying Code Fix:**

```csharp
using System;

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
using Sundstrom.CheckedExceptions;
using System;

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

### 2. **Add `try-catch` Block**

Provides an automated way to wrap code that may throw exceptions within a `try-catch` block, ensuring that exceptions are properly handled.

**Example:**

**Before Applying Code Fix:**

```csharp
using Sundstrom.CheckedExceptions;
using System;

public class Sample
{
    public void Execute()
    {
        // An exception might be thrown but not handled
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
using Sundstrom.CheckedExceptions;
using System;

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
            Console.WriteLine($"Handled exception: {ex.Message}");
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

---

## XML Documentation Support

**CheckedExceptions** leverages XML documentation to identify exceptions from methods that do not have `ThrowsAttribute` annotations. This is particularly useful for:

- **Unannotated Libraries:** Works with libraries that lack explicit exception annotations by using their XML documentation.
- **.NET Class Libraries:** Extends support to the .NET framework by reading exceptions documented in XML.

If a library has both XML docs with exceptions and `ThrowsAttribute` annotations, the analyzer combines exceptions from both sources.

**Example:**

```csharp
using Sundstrom.CheckedExceptions;
using System;
using System.IO;

public class FrameworkSample
{
    public void WriteToConsole()
    {
        // THROW001: Exception `IOException` might be thrown by `Console.WriteLine` but neither caught nor declared via `ThrowsAttribute`.
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

### Annotating Properties (XML)

For properties, exceptions are typically documented within the property's XML comments. We employ heuristic conventions to determine which accessor an exception is associated with:

- **Getter Exceptions:** If the XML comments mention phrases like "get", "gets", or "getting", the exception is attributed to the getter.
- **Setter Exceptions:** If the comments include phrases such as "set", "sets", or "setting", the exception is attributed to the setter.

**Example:**

Assuming this is a third-party library that has not been annotated.

```csharp
using Sundstrom.CheckedExceptions;
using System;

/// <summary>
/// Processes data by managing internal states and handling related exceptions.
/// </summary>
public class DataProcessor
{
    private string? _data;

    /// <summary>
    /// Gets or sets Data.
    /// </summary>
    /// <exception cref="System.InvalidOperationException">
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
            if (_data is null)
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

---

## Suppressing Diagnostics

While it's recommended to adhere to the analyzer's rules for optimal exception handling, there might be scenarios where you need to suppress specific diagnostics.

### Using Pragma Directives

You can suppress diagnostics directly in your code using `#pragma` directives. This method is useful for suppressing warnings in specific code blocks.

**Example: Suppressing a Diagnostic Around a Method Call**

```csharp
#pragma warning disable THROW001 // Unhandled exception
    PerformOperation();
#pragma warning restore THROW001 // Unhandled exception
```

**Example: Suppressing a Diagnostic for a Specific Throw**

```csharp
#pragma warning disable THROW001 // Unhandled exception
    throw new InvalidOperationException();
#pragma warning restore THROW001 // Unhandled exception
```

### Suppressing with Attributes

Alternatively, you can suppress diagnostics using the `[SuppressMessage]` attribute. This approach is beneficial for suppressing warnings at the method or class level.

**Example: Suppressing a Diagnostic for a Method**

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

---

## Configuration

**CheckedExceptions** offers various properties to configure its behavior, allowing you to tailor exception handling to your project's specific needs. These properties can be adjusted using an `.editorconfig` file or by adding a custom settings file.

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
    <PackageReference Include="Sundstrom.CheckedExceptions" Version="1.2.3" />
  </ItemGroup>

</Project>
```

**Notes:**

- **Selective Enforcement:** By specifying only certain warnings in `WarningsAsErrors`, you can enforce stricter rules where necessary while allowing other warnings to remain as warnings.
- **Gradual Adoption:** This approach enables a gradual transition to more disciplined exception handling by focusing on the most critical diagnostics first.

### Configuration via Settings File

In addition to configuring **CheckedExceptions** using `.editorconfig`, you can further customize its behavior by adding a `CheckedExceptions.settings.json` file to your project. This settings file provides granular control over how specific exceptions are reported, allowing you to tailor diagnostics to your project's unique needs.

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
        "System.NotImplementedException": "Throw",
        "System.IO.IOException": "Propagation",
        "System.TimeoutException": "Always"
    }
}
```

There is a JSON schema provided.

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

- **`System.ArgumentNullException`** will be completely ignored by **CheckedExceptions**, meaning no diagnostics will be reported if this exception is thrown or propagated.
- **`System.IO.IOException`** will be treated as informational when it is propagated, allowing you to track its flow without enforcing handling or declaration.

#### Benefits

- **Customized Diagnostics:** Tailor the analyzer's behavior to fit your project's specific needs and coding standards.
- **Reduced Noise:** By silencing or downgrading non-critical exceptions, you can focus on the most impactful diagnostics.
- **Flexible Enforcement:** Gradually adopt stricter exception handling practices by selectively enabling diagnostics for different exception types.

---

## Examples

To demonstrate how **CheckedExceptions** integrates into your project, here are some practical examples covering basic usage and exception handling.

### Basic Usage

**Annotating a Method:**

```csharp
using Sundstrom.CheckedExceptions;
using System;

public class Calculator
{
    [Throws(typeof(DivideByZeroException))]
    public int Divide(int numerator, int denominator)
    {
        if (denominator is 0)
            throw new DivideByZeroException("Denominator cannot be zero.");

        return numerator / denominator;
    }
}
```

### Handling Exceptions Example

**Handling Declared Exceptions:**

```csharp
using Sundstrom.CheckedExceptions;
using System;

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
            // Additional exception handling logic
        }
    }
}
```

In this example, the `Divide` method declares that it can throw a `DivideByZeroException` using `ThrowsAttribute`. The `PerformDivision` method handles this exception, thus complying with the analyzer's requirements.

---

## Contributing

At the project's [repository](https://github.com/marinasundstrom/CheckedExceptions/).

---

## License

This project is licensed under the [MIT License](https://github.com/marinasundstrom/CheckedExceptions/blob/main/LICENSE).
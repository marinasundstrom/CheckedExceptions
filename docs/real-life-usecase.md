# Real-Life Use Case

## Overview

To demonstrate the practical application of **CheckedExceptionsAnalyzer**, we'll explore a **Data Processing Application** scenario. This example showcases how the analyzer enforces explicit exception handling through multiple exceptions, nested `try-catch` blocks, and exception propagation. By following this use case, you'll gain insights into effectively integrating **CheckedExceptionsAnalyzer** into your projects to promote robust and maintainable code.

## Scenario: Data Processing Application

Imagine developing a data processing application with the following components:

1. **DataFetcher**: Retrieves data from an external source (e.g., a file).
2. **DataParser**: Parses the retrieved raw data into a structured format (e.g., XML).
3. **DataProcessor**: Processes the parsed data to perform business logic.
4. **Logger**: Logs messages and errors to the console or a file.
5. **Application**: Orchestrates the entire workflow, managing interactions between components.

Each component can throw different exceptions based on various failure scenarios. Properly handling these exceptions is crucial for building a resilient and maintainable application. **CheckedExceptionsAnalyzer** ensures that all potential exceptions are explicitly handled or declared, promoting disciplined error management.

## Components and Their Exception Handling

### 1. Defining the `ThrowsAttribute`

Before implementing the components, ensure that the `ThrowsAttribute` is defined in your project. This attribute allows methods to declare the exceptions they can throw, enabling **CheckedExceptionsAnalyzer** to enforce explicit exception handling.

```csharp
using System;

namespace System
{
    /// <summary>
    /// Specifies the exceptions that a method can throw.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Delegate, AllowMultiple = true)]
    public class ThrowsAttribute : Attribute
    {
        /// <summary>
        /// Gets the type of the exception that the method can throw.
        /// </summary>
        public Type ExceptionType { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ThrowsAttribute"/> class with the specified exception type.
        /// </summary>
        /// <param name="exceptionType">The type of exception that the method can throw.</param>
        /// <exception cref="ArgumentException">Thrown when the provided type is not assignable from <see cref="Exception"/>.</exception>
        public ThrowsAttribute(Type exceptionType)
        {
            if (!typeof(Exception).IsAssignableFrom(exceptionType))
                throw new ArgumentException("ExceptionType must be an Exception type.");

            ExceptionType = exceptionType;
        }
    }
}
```

### 2. Implementing the Components

#### 2.1. DataFetcher

**Responsibilities**:
- Fetches data from a specified file path.
- Throws exceptions if the file is not found or access is denied.

```csharp
using System;
using System.IO;

namespace DataProcessingApp
{
    /// <summary>
    /// Responsible for fetching data from an external source.
    /// </summary>
    public class DataFetcher
    {
        /// <summary>
        /// Fetches data from the specified file path.
        /// </summary>
        /// <param name="filePath">The path to the data file.</param>
        /// <returns>The raw data as a string.</returns>
        /// <exception cref="FileNotFoundException">Thrown when the specified file does not exist.</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown when access to the file is denied.</exception>
        [Throws(typeof(FileNotFoundException))]
        [Throws(typeof(UnauthorizedAccessException))]
        public string FetchData(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"The file at path {filePath} was not found.");

            try
            {
                return File.ReadAllText(filePath);
            }
            catch (UnauthorizedAccessException)
            {
                throw; // Propagate the exception
            }
        }
    }
}
```

#### 2.2. DataParser

**Responsibilities**:
- Parses raw data into an XML document.
- Throws an exception if the data is not valid XML.

```csharp
using System;
using System.Xml;

namespace DataProcessingApp
{
    /// <summary>
    /// Responsible for parsing raw data into a structured format.
    /// </summary>
    public class DataParser
    {
        /// <summary>
        /// Parses the raw data into an XML document.
        /// </summary>
        /// <param name="rawData">The raw data as a string.</param>
        /// <returns>The parsed XML document.</returns>
        /// <exception cref="XmlException">Thrown when the raw data is not valid XML.</exception>
        [Throws(typeof(XmlException))]
        public XmlDocument Parse(string rawData)
        {
            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(rawData); // May throw XmlException
            return xmlDoc;
        }
    }
}
```

#### 2.3. DataProcessor

**Responsibilities**:
- Processes the parsed data to perform business logic.
- Throws an exception if the XML data is in an unexpected format.

```csharp
using System;
using System.Xml;

namespace DataProcessingApp
{
    /// <summary>
    /// Responsible for processing the parsed data.
    /// </summary>
    public class DataProcessor
    {
        /// <summary>
        /// Processes the XML data and performs necessary operations.
        /// </summary>
        /// <param name="xmlDoc">The parsed XML document.</param>
        /// <exception cref="InvalidOperationException">Thrown when the XML data is in an unexpected format.</exception>
        [Throws(typeof(InvalidOperationException))]
        public void Process(XmlDocument xmlDoc)
        {
            // Example processing logic
            if (xmlDoc == null)
                throw new InvalidOperationException("XML document cannot be null.");

            // Further processing...
        }
    }
}
```

#### 2.4. Logger

**Responsibilities**:
- Logs messages and errors to the console.
- Throws an exception if an I/O error occurs during logging.

```csharp
using System;
using System.IO;

namespace DataProcessingApp
{
    /// <summary>
    /// Responsible for logging messages and errors.
    /// </summary>
    public class Logger
    {
        /// <summary>
        /// Logs a message to the console.
        /// </summary>
        /// <param name="message">The message to log.</param>
        /// <exception cref="IOException">Thrown when an I/O error occurs.</exception>
        [Throws(typeof(IOException))]
        public void Log(string message)
        {
            Console.WriteLine(message); // May throw IOException
        }
    }
}
```

#### 2.5. Application

**Responsibilities**:
- Orchestrates data fetching, parsing, processing, and logging.
- Manages exception handling across components.

```csharp
using System;
using System.IO;
using System.Xml;

namespace DataProcessingApp
{
    /// <summary>
    /// The main application orchestrating data fetching, parsing, and processing.
    /// </summary>
    public class Application
    {
        private readonly DataFetcher _fetcher;
        private readonly DataParser _parser;
        private readonly DataProcessor _processor;
        private readonly Logger _logger;

        public Application()
        {
            _fetcher = new DataFetcher();
            _parser = new DataParser();
            _processor = new DataProcessor();
            _logger = new Logger();
        }

        /// <summary>
        /// Executes the data processing workflow.
        /// </summary>
        /// <param name="filePath">The path to the data file.</param>
        /// <exception cref="UnauthorizedAccessException">Thrown when access to the file is denied.</exception>
        /// <exception cref="IOException">Thrown when an I/O error occurs during logging.</exception>
        [Throws(typeof(UnauthorizedAccessException))]
        [Throws(typeof(IOException))]
        public void Run(string filePath)
        {
            try
            {
                // Fetch Data
                string rawData = _fetcher.FetchData(filePath);

                // Parse Data
                XmlDocument xmlDoc = _parser.Parse(rawData);

                // Process Data
                _processor.Process(xmlDoc);

                // Log Success
                _logger.Log("Data processed successfully.");
            }
            catch (FileNotFoundException ex)
            {
                Console.WriteLine($"Error fetching data: {ex.Message}");
                // Exit the workflow as data cannot be fetched
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine($"Access denied: {ex.Message}");
                throw; // Propagate the exception as it might require higher-level handling
            }
            catch (XmlException ex)
            {
                Console.WriteLine($"Error parsing XML: {ex.Message}");
                // Exit the workflow as data is invalid
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"Processing error: {ex.Message}");
                // Handle processing error locally or decide to propagate
            }
            // IOException from Logger.Log is declared and propagated
        }
    }
}
```

### 3. Program Entry Point

**Responsibilities**:
- Initiates the application workflow.
- Handles critical exceptions that propagate from the `Run` method.

```csharp
using System;

namespace DataProcessingApp
{
    class Program
    {
        static void Main(string[] args)
        {
            var app = new Application();
            try
            {
                app.Run("data.xml");
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine($"Critical error: {ex.Message}");
                // Additional critical handling if necessary
            }
            catch (IOException ex)
            {
                Console.WriteLine($"Logging error: {ex.Message}");
                // Additional critical handling if necessary
            }
        }
    }
}
```

## Analyzing the Example with **CheckedExceptionsAnalyzer**

Let's walk through how **CheckedExceptionsAnalyzer** interacts with this example to enforce explicit exception handling.

### Initial Diagnostics

When compiling the above code with **CheckedExceptionsAnalyzer** integrated, the analyzer performs the following checks:

1. **Method `Run` in `Application` Class:**

   - **Exceptions Thrown:**
     - `FileNotFoundException`
     - `UnauthorizedAccessException`
     - `XmlException`
     - `InvalidOperationException`
     - `IOException`

   - **Exception Handling:**
     - `FileNotFoundException` is caught and handled within `Run`.
     - `UnauthorizedAccessException` is caught and re-thrown.
     - `XmlException` is caught and handled within `Run`.
     - `InvalidOperationException` is caught and handled within `Run`.
     - `IOException` from `_logger.Log` is not caught within `Run` but is declared using `ThrowsAttribute`.

2. **ThrowsAttribute Declarations:**

   - `Run` method declares:
     - `UnauthorizedAccessException`
     - `IOException`

3. **Diagnostics Reported:**

   - **No diagnostics** are reported since all exceptions thrown by `Run` are either caught within the method or declared using `ThrowsAttribute`.

### Resolution Steps

If any exceptions were not handled or declared, **CheckedExceptionsAnalyzer** would flag them using diagnostic codes like `THROW001`. To resolve such diagnostics:

1. **Handle the Exception Locally:**
   - Add appropriate `try-catch` blocks to handle the exception within the method.

2. **Declare the Exception Using `ThrowsAttribute`:**
   - Apply the `ThrowsAttribute` to declare that the method can propagate the exception.

### Enhancing Exception Handling with Nested `try-catch` Blocks

For more complex scenarios, you might have nested `try-catch` blocks to handle exceptions at different levels. Here's how you can enhance the `Run` method to include nested exception handling:

```csharp
using System;
using System.IO;
using System.Xml;

namespace DataProcessingApp
{
    /// <summary>
    /// The main application orchestrating data fetching, parsing, and processing.
    /// </summary>
    public class Application
    {
        private readonly DataFetcher _fetcher;
        private readonly DataParser _parser;
        private readonly DataProcessor _processor;
        private readonly Logger _logger;

        public Application()
        {
            _fetcher = new DataFetcher();
            _parser = new DataParser();
            _processor = new DataProcessor();
            _logger = new Logger();
        }

        /// <summary>
        /// Executes the data processing workflow with nested exception handling.
        /// </summary>
        /// <param name="filePath">The path to the data file.</param>
        /// <exception cref="UnauthorizedAccessException">Thrown when access to the file is denied.</exception>
        /// <exception cref="IOException">Thrown when an I/O error occurs during logging.</exception>
        [Throws(typeof(UnauthorizedAccessException))]
        [Throws(typeof(IOException))]
        public void Run(string filePath)
        {
            try
            {
                try
                {
                    // Fetch Data
                    string rawData = _fetcher.FetchData(filePath);

                    // Parse Data
                    XmlDocument xmlDoc = _parser.Parse(rawData);

                    // Process Data
                    _processor.Process(xmlDoc);

                    // Log Success
                    _logger.Log("Data processed successfully.");
                }
                catch (FileNotFoundException ex)
                {
                    Console.WriteLine($"Error fetching data: {ex.Message}");
                    throw; // Propagate the exception
                }
                catch (XmlException ex)
                {
                    Console.WriteLine($"Error parsing XML: {ex.Message}");
                    // Handle parsing error locally
                }
            }
            catch (FileNotFoundException ex)
            {
                Console.WriteLine("Terminating application due to missing data file.");
                // Additional cleanup or logging
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine($"Critical error: {ex.Message}");
                throw; // Propagate to higher-level handler
            }
            // IOException from Logger.Log is declared and propagated
        }
    }
}
```

**Diagnostics After Enhancement:**

- **`THROW001` (Unhandled Exception):** If `InvalidOperationException` is not caught or declared, **CheckedExceptionsAnalyzer** will flag it.
  
- **Resolution:** Either catch the `InvalidOperationException` within `Run` or declare it using `ThrowsAttribute`.

### Handling Exceptions from Framework Methods

Methods from the .NET framework, such as `Console.WriteLine`, may throw exceptions like `IOException`. Since these framework methods do not include `ThrowsAttribute` annotations, **CheckedExceptionsAnalyzer** relies on XML documentation to identify and manage these exceptions.

**Example with `Console.WriteLine`:**

```csharp
using System;
using System.IO;

namespace DataProcessingApp
{
    /// <summary>
    /// Responsible for logging messages and errors.
    /// </summary>
    public class Logger
    {
        /// <summary>
        /// Logs a message to the console.
        /// </summary>
        /// <param name="message">The message to log.</param>
        /// <exception cref="IOException">Thrown when an I/O error occurs.</exception>
        [Throws(typeof(IOException))]
        public void Log(string message)
        {
            Console.WriteLine(message); // May throw IOException
        }
    }
}
```

**Usage in Application:**

```csharp
using System;
using System.IO;

namespace DataProcessingApp
{
    public class Application
    {
        private readonly Logger _logger;

        public Application()
        {
            _logger = new Logger();
        }

        /// <summary>
        /// Executes the data processing workflow with logging.
        /// </summary>
        /// <param name="filePath">The path to the data file.</param>
        /// <exception cref="UnauthorizedAccessException">Thrown when access to the file is denied.</exception>
        /// <exception cref="IOException">Thrown when an I/O error occurs during logging.</exception>
        [Throws(typeof(UnauthorizedAccessException))]
        [Throws(typeof(IOException))]
        public void Run(string filePath)
        {
            try
            {
                // Fetch, Parse, and Process Data
                // ...

                // Log Success
                _logger.Log("Data processed successfully.");
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine($"Critical error: {ex.Message}");
                throw; // Propagate to higher-level handler
            }
            catch (IOException ex)
            {
                Console.WriteLine($"Logging error: {ex.Message}");
                throw; // Propagate to higher-level handler
            }
        }
    }
}
```

**Outcome:**

- **`Logger.Log`** method declares `IOException` using `ThrowsAttribute`.
- **`Application.Run`** declares `IOException` and handles it appropriately.
- **CheckedExceptionsAnalyzer** ensures that all exceptions thrown by `Run` are either handled or declared, maintaining robust exception handling.

## Summary of Diagnostics and Resolutions

| **Method**               | **Thrown Exceptions**                                         | **Caught Exceptions**                                        | **Declared Exceptions**                     | **Diagnostics**                        |
|--------------------------|---------------------------------------------------------------|--------------------------------------------------------------|---------------------------------------------|----------------------------------------|
| `DataFetcher.FetchData`  | `FileNotFoundException`, `UnauthorizedAccessException`        | Handled in `Application.Run`                                 | `ThrowsAttribute` declares both             | None                                   |
| `DataParser.Parse`       | `XmlException`                                                | Handled in `Application.Run`                                 | `ThrowsAttribute` declares `XmlException`   | None                                   |
| `DataProcessor.Process`  | `InvalidOperationException`                                  | Handled in `Application.Run`                                 | `ThrowsAttribute` declares `InvalidOperationException` | None                                   |
| `Logger.Log`             | `IOException`                                                 | Handled in `Application.Run`                                 | `ThrowsAttribute` declares `IOException`     | None                                   |
| `Application.Run`        | `UnauthorizedAccessException`, `IOException`                  | Catches and handles `UnauthorizedAccessException`, `IOException` | Declares both exceptions                   | Initially none; ensured by declarations |

## Key Takeaways

1. **Explicit Exception Declaration:**
   - Using `ThrowsAttribute` ensures that any exceptions a method might propagate are explicitly declared, making the exception flow clear and enforceable.

2. **Nested `try-catch` Blocks:**
   - Exceptions can be caught at different levels. Inner `try-catch` blocks can handle specific exceptions, while outer blocks can handle more critical or propagated exceptions.
   - When re-throwing exceptions, it's essential to declare them using `ThrowsAttribute` to maintain the integrity of exception handling.

3. **Propagation of Exceptions:**
   - Unhandled exceptions within a method are either caught or declared. **CheckedExceptionsAnalyzer** flags any exceptions that escape without declaration.

4. **Handling Framework Exceptions:**
   - For exceptions thrown by framework methods (like `Console.WriteLine`), **CheckedExceptionsAnalyzer** relies on XML documentation to identify and manage these exceptions, ensuring comprehensive exception handling.

5. **Preventing Unanticipated Errors:**
   - By enforcing explicit exception handling, the analyzer helps prevent runtime errors caused by unhandled exceptions, leading to more robust and maintainable code.

6. **Flexibility with Opt-In Mechanism:**
   - The analyzer treats exceptions as warnings by default, allowing gradual integration into existing projects without overwhelming developers.

## Best Practices for Managing Complex Exception Scenarios

1. **Consistent Use of `ThrowsAttribute`:**
   - Always declare exceptions that a method can propagate. This consistency aids in maintaining clear and predictable exception flows across the codebase.

2. **Specific Exception Types:**
   - Avoid using general exception types like `Exception`. Instead, use more specific exceptions to enhance clarity and facilitate precise exception handling.

3. **Comprehensive XML Documentation:**
   - Maintain thorough XML documentation for all methods, especially those interacting with external libraries or frameworks. This documentation aids analyzers in accurately identifying potential exceptions.

4. **Modular Exception Handling:**
   - Structure your code so that exception handling is as localized as possible. This approach reduces the complexity of exception flows and makes the code easier to understand and maintain.

5. **Regular Code Reviews:**
   - Periodically review your exception handling logic to ensure that all exceptions are appropriately handled or declared, aligning with the project's exception handling strategy.

## Conclusion

The **Real-Life Use Case** of the Data Processing Application illustrates how **CheckedExceptionsAnalyzer** enforces explicit exception handling through `ThrowsAttribute` declarations and disciplined `try-catch` block implementations. By adhering to these practices, developers can build more reliable, maintainable, and robust C#/.NET applications.

For further assistance or to contribute to enhancing this documentation, please refer to the [Contributing](README.md#contributing) section of the project's README.
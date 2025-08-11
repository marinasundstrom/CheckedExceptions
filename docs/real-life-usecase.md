# Real-Life Use Case

## Overview

To demonstrate the practical application of **CheckedExceptionsAnalyzer**, we'll explore a **Data Processing Application** scenario. This example showcases how the analyzer enforces explicit exception handling through `ThrowsAttribute`, ensuring disciplined and maintainable error management.

## Scenario: Data Processing Application

Imagine developing a data processing application with the following components:

1. **DataFetcher**: Retrieves data from an external source (e.g., a file).
2. **DataParser**: Parses the retrieved raw data into a structured format (e.g., XML).
3. **DataProcessor**: Processes the parsed data to perform business logic.
4. **Logger**: Logs messages and errors to the console or a file.
5. **Application**: Orchestrates the entire workflow, managing interactions between components.

Each component can throw specific exceptions based on various failure scenarios. Properly handling these exceptions is crucial for building a resilient and maintainable application. **CheckedExceptionsAnalyzer** ensures that all potential exceptions are explicitly handled or declared, promoting robust exception management.

---

## Components and Their Exception Handling

### 1. Defining the `ThrowsAttribute`

The `ThrowsAttribute` allows methods to declare the exceptions they might throw. Ensure this attribute is defined in your project:

```csharp
using System;
using System.Collections.Generic;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Delegate | AttributeTargets.Property, AllowMultiple = true)]
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

---

### 2. Implementing the Components

#### 2.1. DataFetcher

The **DataFetcher** retrieves data from a file path. It declares potential exceptions:

```csharp
using System;
using System.IO;

namespace DataProcessingApp
{
    public class DataFetcher
    {
        [Throws(
            typeof(FileNotFoundException),
            typeof(UnauthorizedAccessException)]
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
                throw;
            }
        }
    }
}
```

---

#### 2.2. DataParser

The **DataParser** converts raw data into XML. It declares parsing-related exceptions:

```csharp
using System;
using System.Xml;

namespace DataProcessingApp
{
    public class DataParser
    {
        [Throws(typeof(XmlException))]
        public XmlDocument Parse(string rawData)
        {
            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(rawData);
            return xmlDoc;
        }
    }
}
```

---

#### 2.3. DataProcessor

The **DataProcessor** applies business logic to the parsed data. It declares processing-related exceptions:

```csharp
using System;

namespace DataProcessingApp
{
    public class DataProcessor
    {
        [Throws(typeof(InvalidOperationException))]
        public void Process(XmlDocument xmlDoc)
        {
            if (xmlDoc is null)
                throw new InvalidOperationException("XML document cannot be null.");

            // Further processing...
        }
    }
}
```

---

#### 2.4. Logger

The **Logger** handles application logging and declares I/O-related exceptions:

```csharp
using System;
using System.IO;

namespace DataProcessingApp
{
    public class Logger
    {
        [Throws(typeof(IOException))]
        public void Log(string message)
        {
            Console.WriteLine(message);
        }
    }
}
```

---

#### 2.5. Application

The **Application** orchestrates the entire workflow and manages exceptions:

```csharp
using System;
using System.IO;
using System.Xml;

namespace DataProcessingApp
{
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

        [Throws(
            typeof(UnauthorizedAccessException),
            typeof(IOException))]
        public void Run(string filePath)
        {
            try
            {
                string rawData = _fetcher.FetchData(filePath);
                XmlDocument xmlDoc = _parser.Parse(rawData);
                _processor.Process(xmlDoc);
                _logger.Log("Data processed successfully.");
            }
            catch (FileNotFoundException ex)
            {
                Console.WriteLine($"Error fetching data: {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine($"Access denied: {ex.Message}");
                throw;
            }
            catch (XmlException ex)
            {
                Console.WriteLine($"Error parsing XML: {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"Processing error: {ex.Message}");
            }
        }
    }
}
```

---

### 3. Program Entry Point

The **Program** class initiates the workflow and handles critical exceptions:

```csharp
using System;
using System.IO;

namespace DataProcessingApp
{
    class Program
    {
        [Throws(typeof(IOException))]
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
            }
        }
    }
}
```

---

## Key Enhancements Made

1. **Explicit Exception Declaration**:
   - Used `ThrowsAttribute` to explicitly declare all potential exceptions for each method.

2. **Exception Handling**:
   - Managed exceptions at different levels, with critical exceptions being re-thrown for higher-level handling.

3. **Alignment with Guidelines**:
   - Ensured the application aligns with the exception-handling best practices outlined in the CheckedExceptions documentation.

4. **Robustness**:
   - Improved maintainability and clarity of exception flows throughout the application.

By following this updated use case, developers can build reliable applications that adhere to robust exception-handling practices facilitated by **CheckedExceptionsAnalyzer**.
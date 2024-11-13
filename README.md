# Checked exceptions analyzer

Adds checked exceptions to C#/.NET via the ``ThrowsAttribute`` and analyzers and code fixes. 

Similar to in Java. Warnings by default.

Works for: Methods, properties (accessors), constructors, lambda expressions, and local functions.

Supports propagation of the warnings. Also deals with inheritance for exceptions.

Analyzers:
* Unhandled exception (THROW001)
* Unhandled exception thrown (THROW002)

Code fixes:
* Add ThrowsAttribute
* Add try-catch block

Generated with help from ChatGPT.

## ``ThrowsAttribute``

This can be (re-)defined and used:

```csharp
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

## Annotating methods

Annotate methods with one or more ``ThrowsAttribute`` to indicate what exceptions it might throw:

```csharp
public class DataFetcher
{
    [Throws(typeof(NullReferenceException))]
    public void FetchData()
    {
        throw new NullReferenceException("Data source is null.");
    }
}
```

Utilizing the class:

```csharp
var fetcher = new DataFetcher();
```

Test error:

```csharp
// Method 'FetchData' throws exception 'NullReferenceException' which is not handled(THROW001)
fetcher.FetchData();
```

Fix this warning like so by catching the exceptions:

```csharp
try
{
    fetcher.FetchData();
}
catch (NullReferenceException ex)
{
    Console.WriteLine("Handled exception: " + ex.Message);
}
```

Now the warnings won't propagate.

Also handles inheritance, such as base class ``Exception``:

```csharp
try
{
    fetcher.FetchData();
}
catch (Exception ex)
{
    Console.WriteLine("Handled exception: " + ex.Message);
}
```

### Multiple attributes

Here is a method throwing two exceptions:

```csharp
public class DataFetcher2
{
    [Throws(typeof(NullReferenceException))]
    [Throws(typeof(ArgumentException))]
    public void FetchData()
    {
        throw new NullReferenceException("Data source is null.");
    }
}
```

When you don't handle all exceptions:

```csharp
var fetcher = new DataFetcher();

try
{
    // Method 'FetchData' throws exception 'ArgumentException' which is not handled(THROW001)
    fetcher.FetchData();
}
catch (NullReferenceException ex)
{
    Console.WriteLine("Handled exception: " + ex.Message);
}
```

Handling all exceptions:

```csharp
var fetcher = new DataFetcher();

try
{
    fetcher.FetchData();
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

Or catch a base class, such as ``Exception``.

```csharp
var fetcher = new DataFetcher();

try
{
    fetcher.FetchData();
}
catch (Exception ex)
{
    Console.WriteLine("Handled exception: " + ex.Message);
}
```

## To do

Investigate if it is possible to add support to existing framework type members by parsing MS Doc for exceptions.

## Proposed syntax

This would integrate the analyzer into the programming language.

There could be syntax added to the C# programming language.

```csharp
public class DataFetcher
{
    public void FetchData()
        throws NullReferenceException
        throws ArgumentException
    {
        throw new NullReferenceException("Data source is null.");
    }
}
```

## Framework support

The framework library should be annotated for this to work seamlessly.

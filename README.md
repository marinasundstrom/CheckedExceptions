# Checked exceptions analyzer

Adds checked exceptions to C#/.NET via the ``ThrowsAttribute`` and an analyzer. Similar to in Java. As a warning by default.

Works for methods, properties, and constructors.

Supports propagation of the warnings.

Generated with help from ChatGPT.

## Annotation

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
Add support for:

* Lambdas

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

# Checked Exceptions for C#

Take control of exception flow—enforce explicit handling or declaration in C#.

```bash
dotnet add package Sundstrom.CheckedExceptions
```

```csharp
public class Sample
{
    public void Execute()
    {
        Perform(); // ⚠️ THROW001: Unhandled InvalidOperationException
    }

    [Throws(typeof(InvalidOperationException))]
    public void Perform() => throw new InvalidOperationException();
}
```

For full documentation, samples, and FAQ, see the [project repository](https://github.com/marinasundstrom/CheckedExceptions).


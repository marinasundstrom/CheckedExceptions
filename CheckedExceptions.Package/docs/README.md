# CheckedExceptions for C#

**Bring Java-style checked exceptions to C#: enforce handling or declaration.**

<!-- [![Build](https://github.com/marinasundstrom/CheckedExceptions/actions/workflows/ci.yml/badge.svg)](…) -->
[![NuGet](https://img.shields.io/nuget/v/Sundstrom.CheckedExceptions.svg)](https://www.nuget.org/packages/Sundstrom.CheckedExceptions/) ![License](https://img.shields.io/badge/license-MIT-blue.svg)

---

## 🚀 What It Does

CheckedExceptions is a Roslyn analyzer that makes exception handling **explicit**.  
If a method might throw an exception, the caller must either:

- Handle it (with `try/catch`), or
- Declare it (with `[Throws(typeof(...))]`)

✅ Inspired by Java’s checked exceptions.  
⚙️ Fully opt-in.  
💡 Analyzer warnings by default, errors if you choose.

---

## ✅ Quick Example

```csharp
public class Sample
{
    public void Execute()
    {
        // ⚠️ THROW001: Unhandled exception type 'InvalidOperationException'
        Perform();
    }

    [Throws(typeof(InvalidOperationException))]
    public void Perform()
    {
        throw new InvalidOperationException("Oops!");
    }
}
```

✔️ Fix it by **handling**:

```csharp
public void Execute()
{
    try { Perform(); }
    catch (InvalidOperationException) { /* handle */ }
}
```

Or by **declaring**:

```csharp
[Throws(typeof(InvalidOperationException))]
public void Execute()
{
    Perform();
}
```

---

## 🧠 Why Use It?

- Avoid silent exception propagation
- Document intent with `[Throws]` instead of comments
- Enforce better error design across your codebase
- Works with unannotated .NET methods via XML docs
- Plays nice with nullable annotations

---

## 📦 Installation

```bash
dotnet add package Sundstrom.CheckedExceptions
```

And define `ThrowsAttribute` in your project:

```csharp
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Delegate | AttributeTargets.Property, AllowMultiple = true)]
public class ThrowsAttribute : Attribute
{
    public List<Type> ExceptionTypes { get; } = new();
    public ThrowsAttribute(Type exceptionType, params Type[] others) { … }
}
```

Find the full definition [here](https://github.com/marinasundstrom/CheckedExceptions/blob/main/CheckedExceptions.Attribute/ThrowsAttribute.cs).

---

## ⚙️ Configuration

### .editorconfig

```ini
dotnet_diagnostic.THROW001.severity = warning
dotnet_diagnostic.THROW003.severity = warning
```

### `.csproj`

```xml
<PropertyGroup>
  <WarningsAsErrors>nullable,THROW001</WarningsAsErrors>
</PropertyGroup>
```

### JSON Settings

Add `CheckedExceptions.settings.json`:

```json
{
  "ignoredExceptions": [ "System.ArgumentNullException" ],
  "informationalExceptions": {
    "System.IO.IOException": "Propagation",
    "System.TimeoutException": "Always"
  }
}
```

Register in `.csproj`:

```xml
<ItemGroup>
  <AdditionalFiles Include="CheckedExceptions.settings.json" />
</ItemGroup>
```

---

## 🪪 Diagnostic Codes

| ID         | Description |
|------------|-------------|
| `THROW001` | Unhandled exception: must be caught or declared |
| `THROW003` | Avoid general `Exception` in `[Throws]` |
| `THROW004` | Avoid throwing `Exception` directly |
| `THROW005` | Duplicate `[Throws]` declarations |
| `THROW006` | Declared on override, missing from base |
| `THROW007` | Declared on base, missing from override |

---

## 🛠 Code Fixes

- ✅ Add missing `[Throws]`
- 🧯 Add try/catch block
- 🪛 Suppress with `#pragma` or `[SuppressMessage]`

---

## ✨ Advanced Features

- Supports lambdas, local functions, accessors, events
- Analyzes exception inheritance trees
- Merges `[Throws]` with `<exception>` from XML docs
- Handles nullability context (`#nullable enable`)  
- Understands standard library exceptions (e.g. `Console.WriteLine` → `IOException`)

---

## 🤝 Contributing

1. Fork  
2. Create feature branch  
3. Push PR with tests & documentation  
4. ❤️

---

## 📜 License

[MIT](LICENSE)
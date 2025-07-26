# CheckedExceptions for C#

**Bring Java-style checked exceptions to C#: enforce handling or declaration.**

[![Build](https://github.com/marinasundstrom/CheckedExceptions/actions/workflows/ci.yml/badge.svg)](…)   [![NuGet](https://img.shields.io/nuget/v/Sundstrom.CheckedExceptions.svg)](https://www.nuget.org/packages/Sundstrom.CheckedExceptions/) ![License](https://img.shields.io/badge/license-MIT-blue.svg)

[FAQ](#user-content--frequently-asked-questions-faq) • [Change Log](CHANGELOG.md) • [Documentation](docs) 

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
- Avoid confusing [Throws] with `<exception>` — enforce contracts, not just documentation

---

## 📦 Installation

```bash
dotnet add package Sundstrom.CheckedExceptions
```

And define `ThrowsAttribute` in your project:

```csharp
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Delegate, AllowMultiple = true)]
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

The analyzer offers the following automated code fixes:

- ✅ **Add `[Throws]` declaration** – Adds a `[Throws(typeof(...))]` attribute to declare the exception.
- 🧯 **Surround with try/catch** – Wraps the statement in a `try` block with a generated `catch`.
- 🧯 **Add catch to existing try block** – Appends a new `catch` clause to a nearby `try` block.
- 🪛 **Suppress warning** – Adds `#pragma warning disable` or `[SuppressMessage]`.

---

## ✨ Advanced Features

- Supports lambdas, local functions, accessors, events
- Analyzes exception inheritance trees
- Merges `[Throws]` with `<exception>` from XML docs
- Handles nullability context (`#nullable enable`)  
- Understands standard library exceptions (e.g. `Console.WriteLine` → `IOException`)

---

## ❓ Frequently Asked Questions (FAQ)

### ❓ How is this different from Java's checked exceptions?

**Answer:**

Java's checked exceptions are **mandatory** — the compiler enforces them, and every method must declare or handle them. While this promotes visibility, it also leads to friction, boilerplate, and workarounds like `throws Exception`.

This analyzer takes a **modern, flexible approach**:

* ⚠️ **Warnings by default**, not errors — you’re in control.
* ✍️ **Opt-in declaration** using `[Throws]` — only where it matters.
* 🛠️ **Code fixes and suppression** make adoption practical.
* 🔄 **Gradual adoption** — use it for new code, leave legacy code untouched.
* 🎯 **Focused on intention**, not obligation — you declare what callers need to know, not what `int.Parse` might throw.

> ✅ Summary:
> This is *exception design with intent*, not enforcement by force. It improves exception hygiene without the rigidity of Java’s model.

### ❓ Can I use `<exception>` XML documentation tags instead of the `[Throws]` attribute?

**Answer:**

No — for your own code, `<exception>` tags are **not treated as semantic declarations** by the analyzer. While they are useful for documentation and IntelliSense, they are not part of the C# language’s type system and cannot be reliably analyzed or enforced.

Instead, we encourage and require the use of the `[Throws]` attribute for declaring exceptions in a way that is:

- Explicit and machine-readable  
- Suitable for static analysis and enforcement  
- Integrated with code fixes and tooling support

#### 🧩 Interoperability with external libraries

When analyzing external APIs (e.g., referenced .NET assemblies), we **do** recognize `<exception>` tags from their XML documentation — but only for **interop purposes**. That is:

- We treat documented exceptions from public APIs as "declared" when `[Throws]` is not available.
- This helps maintain compatibility without requiring upstream changes.
- However, this behavior **does not apply** to code within your own solution.

> ⚠️ Summary:  
> `<exception>` tags are respected for **interop**, but they are **not a replacement** for `[Throws]` in code you control.

### ❓ What about .NET Standard 2.0 support?

**Answer:**

The analyzer offers **limited support** for projects targeting .NET Standard 2.0. You’ll still get accurate diagnostics for your own code, as well as third-party libraries. However, members defined in the .NET Standard framework may not indicate which exceptions they throw.

This is due to a **technical limitation**: the XML documentation files for .NET Standard assemblies are often incomplete or malformed, making it impossible to extract reliable exception information.

✅ **Recommendation:** Target a modern .NET SDK (e.g., .NET 6 or later) to get full analyzer support, including framework exception annotations.

---

## 🤝 Contributing

1. Fork  
2. Create feature branch  
3. Push PR with tests & documentation  
4. ❤️

---

## 📜 License

[MIT](LICENSE)

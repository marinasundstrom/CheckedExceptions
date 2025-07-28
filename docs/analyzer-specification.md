# Analyzer Specification

## Unhandled exceptions

The diagnostic for `Unhandled exception types` i `THROW001`.

**Case 2: Unhandled thrown expression**

```c#
// THROW001: Unhandled exception type 'InvalidOperationException'
throw new InvalidOperationException();
```

**Case 2: Declared exceptions not handled**

```c#
// THROW001: Unhandled exception type 'InvalidOperationException'
TestMethod();

[Throws(typeof(InvalidOperationException))]
public void TestMethod() 
{
    throw new InvalidOperationException();
}
```

## Handling exceptions

### Exception declarations (`[Throws]`)

By declaring an exception (using the `Throws` attribute) on a method-like member, or a lambda expression, or a local function, you indicate to the consumer that there is an unhandled exception at the call site.

Can be dealt with like this:

```c#
[Throws(typeof(InvalidOperationException))]
public void TestMethod() 
{
    throw new InvalidOperationException();
}
```

### Supported members

The `Throws` attribute can be added to methods, property accessors, lambda expressions, and local functions.

**Methods (and Local functions)**

```c#
[Throws(typeof(InvalidOperationException))]
public void TestMethod() 
{
    throw new InvalidOperationException();
}

// THROW001: Unhandled exception type 'InvalidOperationException'
TestMethod();
```

**Properties**

```c#
public class Test 
{
    public int Prop
    {
        [Throws(typeof(InvalidOperationException))]
        get => throw new InvalidOperationException();
    }
}

var test = new Test();

// THROW001: Unhandled exception type 'InvalidOperationException'
var v = test.Prop;
```

**Lambda expressions**

```c#
[Throws(typeof(OverflowException))]
Func<int, int, int> add = (a, b) => a + b;

// THROW001: Unhandled exception type 'OverflowException'
add(int.MaxValue, 1);
```


### Handling in `try`/`catch`

You can handle exceptions at throw sites using `try` and `catch`:

```c#

public void Test() 
{
    try 
    {
        TestMethod();
    }
    catch (InvalidOperationException invalidOperationException) 
    {
    }
}

[Throws(typeof(InvalidOperationException))]
public void TestMethod() 
{
    throw new InvalidOperationException();
}

```

Exceptions not handled by any `catch` will propagate upwards, until you have to handle with `[Throws]`.

### Inheritance hierarchies

The analyzer handles inheritance hierarchies in catch statements. That way makes sure that exceptions within the`try` block is properly handled.

It even warns when some exception is redundant due to having declared a super type. As with `InvalidOperationException` which is inherited by `ObjectDisposedException`.

## Interop: XML documentation support

TBA

## Special cases

### Expression-bodied property declarations

> **TL;DR;** _We outline special support for expression-bodied properties_

Normally, you should add throws declarations (`[Throws]`) to your property accessors, like so:

```c#
public int TestProp 
{
    [Throws(typeof(InvalidOperationException))]
    get => throw new InvalidOperationException();
}
```

But that is not possible when using an expression body that represents the `get`.

In fact, this will add the `Throws` attribute to the property, not the accessor:

```c#
[Throws(typeof(InvalidOperationException))]
public int TestProp => throw new InvalidOperationException();
```

We do however treat this as valid in the special case when there is only a `get` accessor defined. And this applies both within user-defined code, and for properties consumed third party libraries using this analyzer.
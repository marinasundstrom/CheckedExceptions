using Test2;

namespace Test.Cases;

public class AnnotationsExternalClassLib
{
    public void TestUsingXmlDoc()
    {
        var x = new Class1();
        x.UsingXmlDoc();
    }

    public void TestUsingXmlDoc2()
    {
        try
        {
            var x = new Class1();
            x.UsingXmlDoc();
        }
        catch (ArgumentOutOfRangeException exc)
        {

        }
        catch (StackOverflowException exc)
        {

        }
    }

    public void TestUsingAttributes()
    {
        var x = new Class1();
        x.UsingAttributes();
    }

    public void TestUsingAttributes2()
    {
        try
        {
            var x = new Class1();
            x.UsingAttributes();
        }
        catch (ArgumentOutOfRangeException exc)
        {

        }
        catch (StackOverflowException exc)
        {
            TestFoo();
        }
    }

    /// <summary>
    /// Test
    /// </summary> 
    /// <exception cref="ArgumentOutOfRangeException">Thrown when age is set to be more than 70 years.</exception>
    /// <exception cref="StackOverflowException">Thrown when age is set to be more than 70 years.</exception>
    public void TestFoo()
    {

    }
}
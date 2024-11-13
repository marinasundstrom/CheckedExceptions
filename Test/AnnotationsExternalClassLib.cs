using Test2;

namespace Test;

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

        }
    }

}
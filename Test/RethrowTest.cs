using System.Text;

namespace Test;

public class RethrowTest
{
    [Throws(typeof(InvalidOperationException))]
    public void MethodThatThrows()
    {
        throw new InvalidOperationException();
    }

    [Throws(typeof(ArgumentException))]
    public void MethodThatThrows2()
    {
        throw new ArgumentException();
    }

    //[Throws(typeof(InvalidOperationException))]
    public void Foo1()
    {
        try
        {
            MethodThatThrows();
        }
        catch (InvalidOperationException)
        {
            throw;
        }
    }

    [Throws(typeof(InvalidOperationException))]
    public void Foo12()
    {
        try
        {
            MethodThatThrows();
        }
        catch (InvalidOperationException)
        {
            throw;
        }
    }

    public void Foo2()
    {
        try
        {
            MethodThatThrows();
        }
        catch
        {
            throw;
        }
    }

    [Throws(typeof(Exception))]
    public void Foo22()
    {
        try
        {
            MethodThatThrows();
        }
        catch
        {
            throw;
        }
    }

    public void Foo31()
    {
        try
        {
            MethodThatThrows();
        }
        catch
        {
            throw;
        }
    }

    public void Foo32()
    {
        try
        {
            MethodThatThrows2();
            MethodThatThrows();
        }
        catch
        {
            throw new Exception();

            throw;
        }
    }

    public void Foo34()
    {
        try
        {
            MethodThatThrows2();
            MethodThatThrows();
        }
        catch
        {
            throw new InvalidDataException();

            try
            {
                throw new InvalidDataException();
            }
            catch
            {
                throw new InvalidCastException();
            }
        }
    }


    public void Foo35()
    {
        try
        {
            MethodThatThrows2();
            MethodThatThrows();
        }
        catch
        {
            throw new InvalidProgramException();

            try
            {
                throw new InvalidDataException();
            }
            catch
            {
                throw new FileNotFoundException();
            }
        }
    }

    [Throws(typeof(InvalidOperationException))]
    public void Foo36()
    {
        try
        {
            MethodThatThrows2();
            MethodThatThrows();
        }
        catch (InvalidOperationException)
        {
            throw new InvalidDataException();
        }
        catch
        {
            throw;
        }
    }

    public void Foo37()
    {
        try
        {
            MethodThatThrows();
            MethodThatThrows();
        }
        catch
        {
            throw;
        }
    }

    public void Foo4()
    {
        try
        {
            MethodThatThrows();
        }
        catch (InvalidOperationException exc)
        {

        }
    }

    public int Foo
    {
        [Throws(typeof(InvalidOperationException))]
        set
        {

        }
    }

    //[Throws(typeof(FormatException))]
    public void Foo5()
    {
        try
        {
            int.Parse("Foo");

            StringBuilder sb = new StringBuilder();
            sb.Append("");

            sb.Length = 2;

            List<int> list = new List<int>();
            var x = list[-1];

            Foo = 2;

            MethodThatThrows2();
        }
        catch
        {
            throw;
        }
    }


    //[Throws(typeof(FormatException))]
    public void Foo6()
    {
        try
        {
            var x = new Test();
        }
        catch
        {
            throw;
        }
    }

    public class Test
    {
        [Throws(typeof(FormatException))]
        public Test() { }
    }
}
using System.Security.Cryptography.X509Certificates;

namespace Test;

public class XmlDocWarnings
{
    /// <exception cref="InvalidCastException" />
    public void Method()
    {

    }

    public void WithLocalFunction()
    {

        /// <exception cref="System.InvalidCastException" />
        void Test()
        {

        }
    }

    /// <exception cref="T:.InvalidCastException" />
    public void Method2()
    {

    }

}
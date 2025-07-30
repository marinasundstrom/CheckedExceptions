using System.Security.Cryptography.X509Certificates;

namespace Test;

public class XmlDocWarnings
{
    /// <exception cref="System.InvalidCastException" />
    public void Method()
    {
        IEnumerable<string>? items = null;
        var x = items.First();
    }

    public void WithLocalFunction()
    {

        /// <exception cref="System.InvalidCastException" />
        void Test()
        {
            IEnumerable<string>? items = null;

            var x = items.First();
        }
    }
}
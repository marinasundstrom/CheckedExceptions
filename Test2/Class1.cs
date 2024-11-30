namespace Test2;

/// <summary>
/// Class
/// </summary>
public class Class1
{
    /// <summary>
    /// Test
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when age is set to be more than 70 years.</exception>
    /// <exception cref="StackOverflowException">Thrown when age is set to be more than 70 years.</exception>
    public void UsingXmlDoc()
    {

    }

    /// <summary>
    /// Test
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when age is set to be more than 70 years.</exception>
    /// <exception cref="StackOverflowException">Thrown when age is set to be more than 70 years.</exception>
    [Throws(typeof(ArgumentOutOfRangeException))]
    [Throws(typeof(StackOverflowException))]
    public void UsingAttributes()
    {

    }
}
using Test;

public class IndexerGetter
{
    public class MyArray
    {
        public string this[int x]
        {
            [Throws(typeof(ArgumentNullException))]
            get
            {
                throw new ArgumentNullException("Data is null.");
            }
        }
    }

    public class MyArray2
    {
        [Throws(typeof(InvalidOperationException))]
        public void Foo()
        {
            throw new InvalidOperationException();
        }

        public string this[int x]
        {
            [Throws(typeof(InvalidOperationException))]
            get
            {
                Foo();

                return null;
            }
        }
    }


    public void DisplayData()
    {
        var provider = new MyArray();
        Console.WriteLine(provider[3]);
    }

    public void DisplayData2()
    {
        var provider = new MyArray();
        try
        {
            Console.WriteLine(provider[2]);
        }
        catch (ArgumentNullException ex)
        {
            Console.WriteLine("Handled exception: " + ex.Message);
        }
    }
}

public class IndexerSetter
{
    public class MyArray
    {
        private string data;

        public string this[int x]
        {
            [Throws(typeof(FormatException))]
            set
            {
                if (!IsValid(value))
                    throw new FormatException("Invalid data format.");
                data = value;
            }
        }

        private bool IsValid(string value) => false; // Simulate invalid data
    }

    public void UpdateData()
    {
        var processor = new MyArray();
        processor[2] = "InvalidData";
    }

    public void UpdateData1()
    {
        var processor = new MyArray();
        try
        {
            processor[2] = "InvalidData";
        }
        catch (FormatException ex)
        {
            Console.WriteLine("Handled exception: " + ex.Message);
        }
    }

    public class MultiParamIndexer
    {
        public double this[int x, int y]
        {
            [Throws(typeof(InvalidOperationException))]
            get
            {
                if (x < 0 || y < 0)
                    throw new InvalidOperationException("Coordinates cannot be negative.");
                return x * y;
            }
        }
    }

    public class TestClass
    {
        public void TestMethod()
        {
            var container = new MultiParamIndexer();

            double result = container[-1, 5]; // Should trigger a diagnostic for InvalidOperationException
        }
    }
}
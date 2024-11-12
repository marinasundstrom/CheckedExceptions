using Test;

public class PropertyGetter
{
    public class DataProvider
    {
        public string Data
        {
            [Throws(typeof(ArgumentNullException))]
            get
            {
                throw new ArgumentNullException("Data is null.");
            }
        }
    }


    public void DisplayData()
    {
        var provider = new DataProvider();
        Console.WriteLine(provider.Data);
    }

    public void DisplayData2()
    {
        var provider = new DataProvider();
        try
        {
            Console.WriteLine(provider.Data);
        }
        catch (ArgumentNullException ex)
        {
            Console.WriteLine("Handled exception: " + ex.Message);
        }
    }
}

public class PropertySetter
{
    public class DataProcessor
    {
        private string data;

        public string Data
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
        var processor = new DataProcessor();
        processor.Data = "InvalidData";
    }

    public void UpdateData1()
    {
        var processor = new DataProcessor();
        try
        {
            processor.Data = "InvalidData";
        }
        catch (FormatException ex)
        {
            Console.WriteLine("Handled exception: " + ex.Message);
        }
    }
}
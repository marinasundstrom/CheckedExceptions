namespace ECommerceSystem;

public class InsufficientInventoryException : Exception { }
public class PaymentProcessingException : Exception { }
public class OrderNotFoundException : Exception { }
public class InvalidStockException : Exception
{
    public InvalidStockException(string? message) : base(message)
    {
    }
}
public class UnauthorizedAccessException : Exception
{
    public UnauthorizedAccessException(string? message) : base(message)
    {
    }
}

public class Product
{
    private int _stock;

    public Product(int initialStock)
    {
        _stock = initialStock;
    }

    public int Stock
    {
        // Getter may throw InsufficientInventoryException and UnauthorizedAccessException
        [Throws(typeof(InsufficientInventoryException))]
        [Throws(typeof(UnauthorizedAccessException))]
        get
        {
            if (!HasAccess())
                throw new UnauthorizedAccessException("Access denied to stock information.");

            if (_stock < 0)
                throw new InsufficientInventoryException();

            return _stock;
        }
        // Setter may throw InvalidStockException and UnauthorizedAccessException
        [Throws(typeof(InvalidStockException))]
        [Throws(typeof(UnauthorizedAccessException))]
        set
        {
            if (!HasAccess())
                throw new UnauthorizedAccessException("Access denied to modify stock.");

            if (value < 0)
                throw new InvalidStockException("Stock cannot be negative.");

            _stock = value;
        }
    }

    private bool HasAccess()
    {
        // Simulate access control
        return false; // Access is denied for demonstration purposes
    }
}

public class OrderProcessor
{
    private Product _product = new Product(10);

    // Method may throw multiple exceptions
    [Throws(typeof(InsufficientInventoryException))]
    [Throws(typeof(PaymentProcessingException))]
    [Throws(typeof(InvalidStockException))]
    [Throws(typeof(UnauthorizedAccessException))]
    public void ProcessOrder(int quantity)
    {
        // Check access
        if (!HasUserAccess())
            throw new UnauthorizedAccessException("User is not authorized to process orders.");

        // Check inventory
        if (_product.Stock < quantity)
            throw new InsufficientInventoryException();

        // Simulate payment processing
        if (!ProcessPayment())
            throw new PaymentProcessingException();

        // Update stock
        _product.Stock -= quantity;
    }

    private bool ProcessPayment()
    {
        // Simulate payment failure
        return false;
    }

    private bool HasUserAccess()
    {
        // Simulate user access control
        return false; // Access is denied for demonstration purposes
    }

    // Method may throw multiple exceptions
    [Throws(typeof(InsufficientInventoryException))]
    [Throws(typeof(OrderNotFoundException))]
    [Throws(typeof(InvalidStockException))]
    [Throws(typeof(UnauthorizedAccessException))]
    public void ProcessBatchOrders()
    {
        // Lambda expression that may throw OrderNotFoundException and UnauthorizedAccessException
        var batchProcessor = [Throws(typeof(OrderNotFoundException)), Throws(typeof(UnauthorizedAccessException))] () =>
        {
            FetchOrder(123); // May throw OrderNotFoundException
            if (!HasUserAccess())
                throw new UnauthorizedAccessException("User is not authorized to fetch orders.");
        };

        try
        {
            batchProcessor();
        }
        catch (OrderNotFoundException ex)
        {
            Console.WriteLine("Order not found in batch processing.");
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.WriteLine("Unauthorized access in batch processing.");
        }

        // Local function that may throw InsufficientInventoryException and InvalidStockException
        [Throws(typeof(InsufficientInventoryException))]
        [Throws(typeof(InvalidStockException))]
        [Throws(typeof(UnauthorizedAccessException))]
        void AdjustInventory(int adjustment)
        {
            if (_product.Stock + adjustment < 0)
                throw new InsufficientInventoryException();

            _product.Stock += adjustment; // May throw InvalidStockException
        }

        AdjustInventory(-5); // Analyzer should ensure exceptions are handled or declared

        // Since AdjustInventory may throw exceptions and they're not caught within ProcessBatchOrders,
        // the method must declare both exceptions via ThrowsAttribute
    }

    [Throws(typeof(OrderNotFoundException))]
    public void FetchOrder(int orderId)
    {
        throw new OrderNotFoundException();
    }
}

public class OrderService
{
    private OrderProcessor _processor = new OrderProcessor();

    // Method may throw multiple exceptions
    [Throws(typeof(InsufficientInventoryException))]
    [Throws(typeof(PaymentProcessingException))]
    [Throws(typeof(UnauthorizedAccessException))]
    [Throws(typeof(InvalidStockException))]
    public void StartOrderProcessing()
    {
        _processor.ProcessOrder(5); // Exceptions may be propagated here
    }

    // Method may throw multiple exceptions
    [Throws(typeof(InsufficientInventoryException))]
    [Throws(typeof(OrderNotFoundException))]
    [Throws(typeof(InvalidStockException))]
    [Throws(typeof(UnauthorizedAccessException))]
    public void StartBatchProcessing()
    {
        _processor.ProcessBatchOrders(); // Exceptions may be propagated here
    }
}

class Program
{
    [Throws(typeof(InvalidStockException))]
    [Throws(typeof(UnauthorizedAccessException))]
    static void Main2(string[] args)
    {
        var service = new OrderService();

        try
        {
            service.StartOrderProcessing(); // Should handle or declare multiple exceptions
        }
        catch (InsufficientInventoryException ex)
        {
            Console.WriteLine("Not enough inventory to fulfill order.");
        }
        catch (PaymentProcessingException ex)
        {
            Console.WriteLine("Payment processing failed.");
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.WriteLine("Unauthorized access during order processing.");
        }

        try
        {
            service.StartBatchProcessing(); // Should handle or declare multiple exceptions
        }
        catch (InsufficientInventoryException ex)
        {
            Console.WriteLine("Batch processing failed due to insufficient inventory.");
        }
        catch (OrderNotFoundException ex)
        {
            Console.WriteLine("Order not found during batch processing.");
        }
        catch (InvalidStockException ex)
        {
            Console.WriteLine("Invalid stock during batch processing.");
        }
    }
}

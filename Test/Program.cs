using Test;

try
{
    try
    {
        var httpClient = new HttpClient()
        {
            BaseAddress = new Uri("https://www.scrapethissite.com")
        };
        var str = await httpClient.GetStringAsync("/");

        Console.WriteLine(str);
    }
    catch (ArgumentException)
    {

    }
    catch (InvalidOperationException)
    {

    }
    catch (HttpRequestException)
    {

    }
    catch (TaskCanceledException)
    {

    }
    catch (UriFormatException)
    {

    }
}
catch (IOException)
{

}
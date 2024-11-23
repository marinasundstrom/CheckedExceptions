using Test;


try
{
    var httpClient = new HttpClient()
    {
        BaseAddress = new Uri("https://www.scrapethissite.com")
    };
    var str = await httpClient.GetStringAsync("/");

    Console.WriteLine(str);
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

using System.Net.Http;
using System.Runtime.CompilerServices;

namespace ConsoleHttpRequest;

public static class WasiMainWrapper
{
    public static async Task<int> MainAsync(string[] args)
    {
        using HttpClient client = new();
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Add("User-Agent", "dotnet WASI");
        
        string url = "https://www.random.org/integers/?num=1&min=40&max=42&col=1&base=10&format=plain&rnd=new";
        string response = await client.GetStringAsync(url);
        
        Console.WriteLine(response);

        return 0;
    }

    public static int Main(string[] args)
    {
        // Required to run async code in WASM until threading support is added
        return PollWasiEventLoopUntilResolved((Thread)null!, MainAsync(args));

        [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "PollWasiEventLoopUntilResolved")]
        static extern T PollWasiEventLoopUntilResolved<T>(Thread t, Task<T> mainTask);
    }
}
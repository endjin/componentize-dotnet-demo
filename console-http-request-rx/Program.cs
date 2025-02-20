using System.Runtime.CompilerServices;
using System.Reactive.Linq;

public static class WasiMainWrapper
{
    public static async Task<int> MainAsync(string[] args)
    {
        using HttpClient client = new();
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Add("User-Agent", "dotnet WASI");
        
        string url = $"https://www.random.org/integers/?num=1&min=40&max=42&col=1&base=10&format=plain&rnd=new";

        var observable = Observable.Range(0, 5)
            .SelectMany(_ => Observable.FromAsync(() => client.GetStringAsync(url)))
            .Select(response => int.Parse(response.Trim()));

        var tcs = new TaskCompletionSource<bool>();

        observable.Subscribe(
            number => {
                if (number == 42)
                {
                    Console.WriteLine("The Answer to the Ultimate Question of Life, the Universe, and Everything is 42");
                }
                else
                {
                    Console.WriteLine($"Received: {number}");
                }
            },
            ex => {
                Console.WriteLine($"Error: {ex.Message}");
                tcs.SetResult(false);
            },
            () => {
                Console.WriteLine("Completed fetching random numbers");
                tcs.SetResult(true);
            }
        );

        await tcs.Task;
        
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
using System.Runtime.CompilerServices;
using System.Reactive.Linq;

namespace ConsoleHttpRequestRx;

public static class WasiMainWrapper
{
    private static readonly string[] UserAgentHeaders = ["dotnet", "WASI"];

    public static async Task<int> MainAsync(string[] args)
    {
        try
        {
            using HttpClient client = new();
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Add("User-Agent", UserAgentHeaders);

            const string url = "https://www.random.org/integers/?num=1&min=40&max=42&col=1&base=10&format=plain&rnd=new";

            var observable = Observable.Range(0, 5)
                .SelectMany(async _ => await client.GetStringAsync(url))
                .Select(static response => int.Parse(response.Trim()));

            var tcs = new TaskCompletionSource<bool>();

            observable.Subscribe(
                onNext: static number => Console.WriteLine(
                    number == 42 
                        ? "The Answer to the Ultimate Question of Life, the Universe, and Everything is 42"
                        : $"Received: {number}"),
                onError: ex => 
                {
                    Console.WriteLine($"Error at {DateTimeOffset.UtcNow}: {ex.Message}");
                    tcs.SetResult(false);
                },
                onCompleted: () => 
                {
                    Console.WriteLine("Completed fetching random numbers");
                    tcs.SetResult(true);
                });

            await tcs.Task;
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fatal error: {ex.Message}");
            return 1;
        }
    }

    public static int Main(string[] args)
    {
        // Required to run async code in WASM until threading support is added
        return PollWasiEventLoopUntilResolved((Thread)null!, MainAsync(args));

        [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "PollWasiEventLoopUntilResolved")]
        static extern T PollWasiEventLoopUntilResolved<T>(Thread t, Task<T> mainTask);
    }
}
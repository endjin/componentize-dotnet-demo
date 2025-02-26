using Ais.Net.Models.Abstractions;
using Ais.Net.Receiver.Receiver;

using System.Reactive.Linq;
using System.Runtime.CompilerServices;

namespace Ais.Net.Receiver.Host.Wasi;

public static class Program
{
    public static async Task<int> MainAsync(string[] args)
    {
        INmeaReceiver receiver = new NetworkStreamNmeaReceiver(new WasiSocketNmeaStreamReader(), host: "153.44.253.27", port: 5631, retryAttemptLimit: 100, retryPeriodicity: TimeSpan.Parse("00:00:00:00.500"));
       
        ReceiverHost receiverHost = new (receiver);

        receiverHost.Messages.Subscribe(message => 
        {
            Console.WriteLine($"Received message: {message}");
        });

         receiverHost.Errors.Subscribe(error =>
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error received: {error.Exception.Message}");
            Console.WriteLine($"Bad line: {error.Line}");
            Console.ResetColor();
        });

        IObservable<IGroupedObservable<uint, IAisMessage>> byVessel = receiverHost.Messages.GroupBy(m => m.Mmsi);

        IObservable<(uint mmsi, IVesselNavigation navigation, IVesselName name, ShipType ShipType)> vesselNavigationWithNameStream =
            from perVesselMessages in byVessel
            let vesselNavigationUpdates = perVesselMessages.OfType<IVesselNavigation>()
            let vesselNames = perVesselMessages.OfType<IVesselName>()
            let shipTypes = perVesselMessages.OfType<IShipType>()
            let vesselLocationsWithNames = vesselNavigationUpdates.CombineLatest(vesselNames, shipTypes, (navigation, name, shipType) => (navigation, name, shipType))
            from vesselLocationAndName in vesselLocationsWithNames
            select (mmsi: perVesselMessages.Key, vesselLocationAndName.navigation, vesselLocationAndName.name, vesselLocationAndName.shipType.ShipType);

        vesselNavigationWithNameStream.Subscribe(navigationWithName =>
        {
            (uint mmsi, IVesselNavigation navigation, IVesselName name, ShipType shipType) = navigationWithName;

            Console.WriteLine($"MMSI: {mmsi}, Name: {name.VesselName}, Lat: {navigation.Position?.Latitude}, Lon: {navigation.Position?.Longitude}, COG: {navigation.CourseOverGround}, ShipType: {shipType}");
        });

        CancellationTokenSource cts = new();

        Console.WriteLine("Start the receiver");
        await receiverHost.StartAsync(cts.Token);
        Console.WriteLine("Receiver started");
       
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
using Ais.Net;
using Ais.Net.Models;
using Ais.Net.Models.Abstractions;
using Ais.Net.Receiver.Configuration;
using Ais.Net.Receiver.Receiver;

using System.Reactive; 
using System.Reactive.Linq;

using System.Runtime.CompilerServices;

public static class WasiMainWrapper
{
    public static async Task<int> MainAsync(string[] args)
    {
        INmeaReceiver receiver = new NetworkStreamNmeaReceiver(host: "153.44.253.27", port: 5631, retryAttemptLimit: 100, retryPeriodicity: TimeSpan.Parse("00:00:00:00.500"));
        ReceiverHost receiverHost = new (receiver);

        receiverHost.Messages.Subscribe(message => 
        {
            Console.WriteLine($"Received message: {message}");
        });

        IObservable<IGroupedObservable<uint, IAisMessage>> byVessel = receiverHost.Messages.GroupBy(m => m.Mmsi);

        var vesselNavigationWithNameStream =
            from perVesselMessages in byVessel
            let vesselNavigationUpdates = perVesselMessages.OfType<IVesselNavigation>()
            let vesselNames = perVesselMessages.OfType<IVesselName>()
            let shipTypes = perVesselMessages.OfType<IShipType>()
            let vesselLocationsWithNames = Observable.CombineLatest(vesselNavigationUpdates, vesselNames, shipTypes, (navigation, name, shipType) => (navigation, name, shipType))
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

    private static string ToHex(ShipTypeCategory shipTypeCategory)
    {
        return shipTypeCategory switch
        {
            ShipTypeCategory.NotAvailable => "#96F9A1",
            ShipTypeCategory.Reserved => "#1C79F0",
            ShipTypeCategory.WingInGround => "#F8BA97",
            ShipTypeCategory.SpecialCategory3 => "#F8B594",
            ShipTypeCategory.HighSpeedCraft => "#FFFF55",
            ShipTypeCategory.SpecialCategory5 => "#43FFFF",
            ShipTypeCategory.Passenger => "#203DB3",
            ShipTypeCategory.Cargo => "#97F9A1",
            ShipTypeCategory.Tanker => "#FF464E",
            ShipTypeCategory.Other => "#56FFFF",
            _ => "#96F9A1",
        };
    }

    public static int Main(string[] args)
    {
        // Required to run async code in WASM until threading support is added
        return PollWasiEventLoopUntilResolved((Thread)null!, MainAsync(args));

        [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "PollWasiEventLoopUntilResolved")]
        static extern T PollWasiEventLoopUntilResolved<T>(Thread t, Task<T> mainTask);
    }
}
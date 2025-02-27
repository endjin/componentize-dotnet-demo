using Ais.Net.Models.Abstractions;
using Ais.Net.Receiver.Host.Wasi.Logging;
using Ais.Net.Receiver.Receiver;

using System.Reactive.Linq;
using System.Runtime.CompilerServices;

namespace Ais.Net.Receiver.Host.Wasi;

public static class Program
{
    public static async Task<int> MainAsync(string[] args)
    {
        LogLevel logLevel = LogLevel.Info;

        ConsoleLogger logger = new() { MinimumLevel = logLevel };

        logger.Debug($"Starting with log level: {logLevel}");
        logger.Debug($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] Starting AIS receiver");

        INmeaReceiver receiver = new NetworkStreamNmeaReceiver(
            new WasiSocketNmeaStreamReader(logger),
            host: "153.44.253.27",
            port: 5631,
            retryAttemptLimit: 100,
            retryPeriodicity: TimeSpan.Parse("00:00:00:00.500"));

        ReceiverHost receiverHost = new(receiver);

        receiverHost.Messages.Subscribe(message =>
             logger.Debug($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] Message Type: {message.GetType().Name}, Raw: {message}")
        );

        receiverHost.Errors.Subscribe(error =>
        {
            logger.Debug($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] Error Type: {error.Exception.GetType().Name}");
            logger.Debug($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] Error Message: {error.Exception.Message}");
            logger.Debug($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] Bad Line: {error.Line}");
            logger.Debug($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] Stack Trace: {error.Exception.StackTrace}");
        });
       
        IObservable<IGroupedObservable<uint, IAisMessage>> byVessel = receiverHost.Messages.GroupBy(m => m.Mmsi);

        IObservable<(uint mmsi, IVesselNavigation navigation, IVesselName name, ShipType ShipType)> vesselNavigationWithNameStream =
            from perVesselMessages in byVessel
            let vesselNavigationUpdates = perVesselMessages.OfType<IVesselNavigation>()
            let vesselNames = perVesselMessages.OfType<IVesselName>()
            let shipTypes = perVesselMessages.OfType<IShipType>()
            let vesselLocationsWithNames = 
                vesselNavigationUpdates.CombineLatest(vesselNames, shipTypes, (navigation, name, shipType) => (navigation, name, shipType))
            from vesselLocationAndName in vesselLocationsWithNames
            select (mmsi: perVesselMessages.Key, vesselLocationAndName.navigation, vesselLocationAndName.name, vesselLocationAndName.shipType.ShipType);

        vesselNavigationWithNameStream.Subscribe(
            navigationWithName =>
            {
                (uint mmsi, IVesselNavigation navigation, IVesselName name, ShipType shipType) = navigationWithName;
                logger.Info($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] Vessel Update: MMSI={mmsi}, Name={name.VesselName.Replace("@", string.Empty).Trim()}, Pos=({navigation.Position?.Latitude},{navigation.Position?.Longitude}), Course={navigation.CourseOverGround}, Type={shipType}");
            },
            error =>
            {
                // Lots of the data received is not valid, so we don't want to log this as an error
                logger.Debug($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] Vessel Stream Error: {error.GetType().Name}");
                logger.Debug($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] Error Message: {error.Message}");
                logger.Debug($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] Stack Trace: {error.StackTrace}");
            });

        using CancellationTokenSource cts = new();

        logger.Debug("Start the receiver");

        await receiverHost.StartAsync(cts.Token);

        logger.Debug($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] AIS receiver started successfully");

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
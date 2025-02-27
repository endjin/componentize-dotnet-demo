namespace Ais.Net.Receiver.Host.Wasi.Logging;

public class ConsoleLogger : ILogger
{
    public LogLevel MinimumLevel { get; set; } = LogLevel.Info;

    private readonly string timeStampFormat = "yyyy-MM-dd HH:mm:ss.fff";
    
    public void Debug(string message)
    {
        if (MinimumLevel <= LogLevel.Debug)
            WriteLine(LogLevel.Debug, message);
    }
    
    public void Info(string message)
    {
        if (MinimumLevel <= LogLevel.Info)
            WriteLine(LogLevel.Info, message);
    }
    
    public void Warning(string message)
    {
        if (MinimumLevel <= LogLevel.Warning)
            WriteLine(LogLevel.Warning, message);
    }
    
    public void Error(string message)
    {
        if (MinimumLevel <= LogLevel.Error)
            WriteLine(LogLevel.Error, message);
    }
    
    public void Critical(string message, Exception? exception = null)
    {
        if (MinimumLevel <= LogLevel.Critical)
        {
            WriteLine(LogLevel.Critical, message);
            if (exception != null)
                WriteLine(LogLevel.Critical, $"Exception: {exception.GetType().Name} - {exception.Message}\n{exception.StackTrace}");
        }
    }
    
    private void WriteLine(LogLevel level, string message)
    {
        string timestamp = DateTime.UtcNow.ToString(this.timeStampFormat);
        Console.WriteLine($"[{timestamp}] [{level}] {message}");
    }
}
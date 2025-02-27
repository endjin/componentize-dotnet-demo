namespace Ais.Net.Receiver.Host.Wasi.Logging;

public interface ILogger
{
    void Debug(string message);
    void Info(string message);
    void Warning(string message);
    void Error(string message);
    void Critical(string message, Exception? exception = null);
    LogLevel MinimumLevel { get; set; }
}

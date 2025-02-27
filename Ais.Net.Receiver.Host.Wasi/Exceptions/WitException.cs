using System;

namespace Ais.Net.Receiver.Host.Wasi.Exceptions;

/// <summary>
/// Exception that represents errors originating from WIT/WASI operations
/// </summary>
public class WitException : Exception
{
    /// <summary>
    /// Gets the error value associated with this exception
    /// </summary>
    public object? Value { get; }

    public WitException() : base() { }
    
    public WitException(string message) : base(message) { }
    
    public WitException(string message, Exception innerException) 
        : base(message, innerException) { }

    public WitException(object? value) : base()
    {
        Value = value;
    }

    public WitException(string message, object? value) : base(message)
    {
        Value = value;
    }

    public WitException(string message, object? value, Exception innerException)
        : base(message, innerException)
    {
        Value = value;
    }
}

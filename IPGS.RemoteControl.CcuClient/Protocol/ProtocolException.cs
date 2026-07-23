namespace IPGS.RemoteControl.Protocol;

/// <summary>
/// Thrown when the remote control TCP protocol is violated (bad message type, oversized payload,
/// unexpected handshake sequence, etc.).
/// </summary>
public sealed class ProtocolException : Exception
{
    public ProtocolException(string message) : base(message) { }
    public ProtocolException(string message, Exception inner) : base(message, inner) { }
}

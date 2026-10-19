using System;
using System.Collections.Generic;

public sealed class AsyncEchoTransportResult
{
    public string Json { get; }
    public string ErrorCode { get; }
    public bool Retryable { get; }
    public AsyncEchoTransportResult(string json, string errorCode = null, bool retryable = false)
    { Json = json; ErrorCode = errorCode; Retryable = retryable; }
}
/// <summary>No SDK type or local-save access crosses this boundary.</summary>
public interface IAsyncEchoTransport
{
    bool IsAvailable { get; }
    string UnavailableReason { get; }
    void Send(Dictionary<string, object> request, Action<AsyncEchoTransportResult> completed);
}
public sealed class UnavailableAsyncEchoTransport : IAsyncEchoTransport
{
    public bool IsAvailable => false;
    public string UnavailableReason { get; }
    public UnavailableAsyncEchoTransport(string reason = "ASYNC_UNAVAILABLE")
    { UnavailableReason = reason; }
    public void Send(Dictionary<string, object> request, Action<AsyncEchoTransportResult> completed)
    { completed?.Invoke(new AsyncEchoTransportResult(null, UnavailableReason)); }
}

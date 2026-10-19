using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class AsyncEchoCloudSettings
{
    public bool featureEnabled;
    public string cloudEnvironmentId;
    public string functionName = "echo";

    public static AsyncEchoCloudSettings Load()
    {
        TextAsset asset = Resources.Load<TextAsset>("Weixin/AsyncEchoCloudSettings");
        if (asset == null) return new AsyncEchoCloudSettings();
        try { return JsonUtility.FromJson<AsyncEchoCloudSettings>(asset.text) ?? new AsyncEchoCloudSettings(); }
        catch (ArgumentException) { return new AsyncEchoCloudSettings(); }
    }
}

/// <summary>Fixed official SDK adapter; no hand-written JS bridge or SDK retry wrapper.</summary>
public sealed class WeixinAsyncEchoTransport : IAsyncEchoTransport
{
    private readonly AsyncEchoCloudSettings _settings;
    private bool _ready;
    public bool IsAvailable => _ready;
    public string UnavailableReason { get; private set; } = "ASYNC_UNAVAILABLE";

    public WeixinAsyncEchoTransport(AsyncEchoCloudSettings settings)
    { _settings = settings ?? new AsyncEchoCloudSettings(); }

    public void InitializeCloud()
    {
#if MINIGAME_SUBPLATFORM_WEIXIN && !UNITY_EDITOR
        if (_ready) return;
        if (!_settings.featureEnabled || string.IsNullOrWhiteSpace(_settings.cloudEnvironmentId)
            || string.IsNullOrWhiteSpace(_settings.functionName))
        { UnavailableReason = "CLOUD_NOT_CONFIGURED"; return; }
        try
        {
            WeChatWASM.WX.cloud.Init(new WeChatWASM.ICloudConfig
                { env = _settings.cloudEnvironmentId, traceUser = false });
            _ready = true;
            UnavailableReason = null;
        }
        catch (Exception) { UnavailableReason = "CLOUD_NOT_CONFIGURED"; }
#endif
    }

    public void Send(Dictionary<string, object> request, Action<AsyncEchoTransportResult> completed)
    {
        if (!_ready)
        { completed?.Invoke(new AsyncEchoTransportResult(null, UnavailableReason)); return; }
#if MINIGAME_SUBPLATFORM_WEIXIN && !UNITY_EDITOR
        try
        {
            WeChatWASM.WX.cloud.CallFunction(new WeChatWASM.CallFunctionParam
            {
                name = _settings.functionName,
                data = request,
                config = new WeChatWASM.ICloudConfig { env = _settings.cloudEnvironmentId },
                success = result => completed?.Invoke(new AsyncEchoTransportResult(result.result)),
                fail = result => completed?.Invoke(new AsyncEchoTransportResult(null,
                    ClassifySdkError(result.errMsg), IsTransient(result.errMsg)))
            });
        }
        catch (Exception) { completed?.Invoke(new AsyncEchoTransportResult(null, "NETWORK_ERROR", true)); }
#else
        completed?.Invoke(new AsyncEchoTransportResult(null, "ASYNC_UNAVAILABLE"));
#endif
    }

    public bool TryShare(AsyncEchoInvitation invitation)
    {
        if (!_ready || invitation == null) return false;
#if MINIGAME_SUBPLATFORM_WEIXIN && !UNITY_EDITOR
        try
        {
            WeChatWASM.WX.ShareAppMessage(new WeChatWASM.ShareAppMessageOption
                { title = "来挑战我的冻结回声", query = invitation.ToQuery() });
            // This proves only that the share UI was requested, never delivery.
            return true;
        }
        catch (Exception) { return false; }
#else
        return false;
#endif
    }

    private static bool IsTransient(string error)
    {
        string message = (error ?? "").ToLowerInvariant();
        return message.Contains("timeout") || message.Contains("network")
            || message.Contains("connect") || message.Contains("temporar");
    }
    private static string ClassifySdkError(string error)
        => IsTransient(error) ? "NETWORK_ERROR" : "CLOUD_UNAVAILABLE";
}

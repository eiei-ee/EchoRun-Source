using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>Tick-driven optional service. SDK callbacks never run gameplay or UI code.</summary>
public sealed class AsyncEchoCloud : IDisposable
{
    private sealed class Operation
    {
        public string key;
        public Dictionary<string, object> request;
        public int attempt;
        public double deadline, attemptDeadline;
        public Action<AsyncEchoCloudReply> completed;
    }
    private sealed class Completion
    {
        public Operation operation;
        public int attempt;
        public AsyncEchoTransportResult result;
    }
    private readonly IAsyncEchoTransport _transport;
    private readonly Func<double> _clock;
    private readonly Dictionary<string, Operation> _operations = new Dictionary<string, Operation>();
    private readonly Queue<Completion> _completions = new Queue<Completion>();
    private readonly object _queueLock = new object();
    private bool _disposed, _menuAvailable, _pendingInvitation;
    private int _invitationEpoch, _publishEpoch, _leaderboardEpoch;
    private string _lastAutomaticInvitation, _publishJson, _lastReportKey;
    private Dictionary<string, object> _lastReportRequest;
    private int _lastReportRules;

    public event Action Changed;
    public event Action<string> Notice;
    public bool IsAvailable => !_disposed && _transport.IsAvailable;
    public AsyncEchoInvitation RecentInvitation { get; private set; }
    public AsyncEchoSnapshot CurrentSnapshot { get; private set; }
    public bool InvitationVisible { get; private set; }
    public AsyncEchoPublishReceipt Published { get; private set; }
    public AsyncEchoOperationState PublishState { get; private set; }
    public AsyncEchoOperationState InvitationState { get; private set; }
    public AsyncEchoOperationState ReportState { get; private set; }
    public AsyncEchoOperationState LeaderboardState { get; private set; }
    public string PublishError { get; private set; }
    public string InvitationError { get; private set; }
    public string ReportError { get; private set; }
    public string LeaderboardError { get; private set; }
    public string LeaderboardBoardId { get; private set; }
    public AsyncEchoLeaderboardEntry[] Leaderboard { get; private set; } = new AsyncEchoLeaderboardEntry[0];
    public string LastReportBoardId => _lastReportRequest != null ? (string)_lastReportRequest["boardId"] : null;
    public int InFlightCount => _operations.Count;

    public AsyncEchoCloud(IAsyncEchoTransport transport, Func<double> clock)
    {
        _transport = transport ?? new UnavailableAsyncEchoTransport();
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public void SetMenuAvailable(bool available)
    {
        _menuAvailable = available;
        if (available && _pendingInvitation) OpenRecentInvitation();
    }

    public void ReceiveInvitation(AsyncEchoInvitation invitation)
    {
        if (invitation == null || _disposed) return;
        RecentInvitation = invitation;
        if (_lastAutomaticInvitation == invitation.Key) { Changed?.Invoke(); return; }
        _lastAutomaticInvitation = invitation.Key;
        _pendingInvitation = true;
        InvalidateGet();
        if (_menuAvailable) OpenRecentInvitation();
        else { Notice?.Invoke("INVITATION_WAITING"); Changed?.Invoke(); }
    }

    public void OpenRecentInvitation()
    {
        if (RecentInvitation == null || _disposed || !_menuAvailable) return;
        if (InvitationVisible && _operations.ContainsKey("get")) return;
        _pendingInvitation = false;
        InvalidateGet();
        InvitationVisible = true;
        CurrentSnapshot = null;
        InvitationError = null;
        InvitationState = AsyncEchoOperationState.Pending;
        int epoch = _invitationEpoch;
        AsyncEchoInvitation invitation = RecentInvitation;
        var request = Request("get");
        request["inviterOpenid"] = invitation.InviterOpenid;
        request["identityId"] = invitation.IdentityId;
        request["rulesVersion"] = invitation.RulesVersion;
        Start("get", request, reply =>
        {
            if (epoch != _invitationEpoch) return;
            ActiveEchoIdentity identity;
            string error;
            var data = reply.data;
            if (!reply.ok) error = reply.error.code;
            else if (!data.found) error = "SHADOW_NOT_FOUND";
            else if (data.ownerOpenid != invitation.InviterOpenid
                || data.identityId != invitation.IdentityId
                || data.rulesVersion != invitation.RulesVersion
                || data.payloadVersion != ActiveEchoIdentity.CurrentVersion
                || !AsyncEchoInvitation.IsValidId(data.boardId) || data.runSeed <= 0)
                error = "INVALID_RESPONSE";
            else if (ActiveEchoIdentity.TryFromExternalJson(data.payloadJson, out identity, out error)
                && identity.identityId == invitation.IdentityId && identity.generation == data.generation)
            {
                CurrentSnapshot = new AsyncEchoSnapshot(data.boardId, invitation, data.runSeed, identity);
                InvitationState = AsyncEchoOperationState.Succeeded;
                Changed?.Invoke();
                return;
            }
            else if (string.IsNullOrEmpty(error)) error = "INVALID_RESPONSE";
            InvitationError = error;
            InvitationState = FailureState(error);
            Notice?.Invoke(error);
            Changed?.Invoke();
        });
        Changed?.Invoke();
    }

    public void DismissInvitation()
    {
        InvalidateGet();
        _pendingInvitation = false;
        InvitationVisible = false;
        CurrentSnapshot = null;
        InvitationState = AsyncEchoOperationState.Idle;
        Changed?.Invoke();
    }

    /// <summary>Starting/reloading a run closes its old get but preserves a newer queued invite.</summary>
    public void RunStarted()
    {
        _menuAvailable = false;
        InvalidateGet();
        InvitationVisible = false;
        CurrentSnapshot = null;
        InvitationState = AsyncEchoOperationState.Idle;
        Changed?.Invoke();
    }

    private void InvalidateGet()
    { _invitationEpoch++; _operations.Remove("get"); }

    public void Publish(ActiveEchoIdentity identity)
    {
        if (_disposed) return;
        string raw = identity != null ? JsonUtility.ToJson(identity) : null;
        ActiveEchoIdentity validated;
        string error;
        if (!ActiveEchoIdentity.TryFromExternalJson(raw, out validated, out error))
        {
            _publishEpoch++;
            _operations.Remove("publish");
            _publishJson = null;
            PublishError = error ?? "NO_CHALLENGE_IDENTITY";
            PublishState = AsyncEchoOperationState.Failed;
            Published = null;
            Changed?.Invoke();
            return;
        }
        string json = validated.ToJson();
        if (json == _publishJson && (PublishState == AsyncEchoOperationState.Pending
            || PublishState == AsyncEchoOperationState.Succeeded)) return;
        _publishJson = json;
        Published = null;
        _operations.Remove("publish");
        int epoch = ++_publishEpoch;
        PublishState = AsyncEchoOperationState.Pending;
        PublishError = null;
        var request = Request("publish");
        request["payloadJson"] = json;
        request["rulesVersion"] = AsyncEchoInvitation.SupportedRulesVersion;
        Start("publish", request, reply =>
        {
            if (epoch != _publishEpoch) return;
            var data = reply.data;
            if (reply.ok && data.identityId == validated.identityId
                && data.generation == validated.generation && data.rulesVersion == 1
                && data.runSeed > 0 && AsyncEchoInvitation.IsValidId(data.boardId)
                && AsyncEchoInvitation.IsValidId(data.inviterOpenid))
            {
                Published = new AsyncEchoPublishReceipt(data.boardId,
                    new AsyncEchoInvitation(data.inviterOpenid, data.identityId, data.rulesVersion));
                PublishState = AsyncEchoOperationState.Succeeded;
            }
            else
            {
                PublishError = reply.ok ? "INVALID_RESPONSE" : reply.error.code;
                PublishState = FailureState(PublishError);
                Notice?.Invoke(PublishError);
            }
            Changed?.Invoke();
        });
        Changed?.Invoke();
    }

    public void Report(AsyncChallengeResult result, AsyncEchoSnapshot snapshot)
    {
        if (result == null || snapshot == null || _disposed) return;
        if (result.opponentIdentityId != snapshot.Invitation.IdentityId
            || result.rulesVersion != snapshot.Invitation.RulesVersion || result.runSeed != snapshot.RunSeed)
        { Notice?.Invoke("CHALLENGE_CONTEXT_MISMATCH"); return; }
        var request = Request("report");
        request["boardId"] = snapshot.BoardId;
        request["challengeId"] = result.challengeId;
        request["rulesVersion"] = result.rulesVersion;
        request["distanceMeters"] = result.distanceMeters;
        request["playerLeadMeters"] = result.playerLeadMeters;
        request["endReason"] = result.reason == RunEndReason.FinishReached ? "finish_reached"
            : result.reason == RunEndReason.Collision ? "collision" : "abandoned";
        request["playerWon"] = result.won;
        _lastReportRequest = request;
        _lastReportRules = result.rulesVersion;
        _lastReportKey = "report:" + result.challengeId;
        SendLastReport();
    }

    public void RetryReport() { if (_lastReportRequest != null) SendLastReport(); }

    private void SendLastReport()
    {
        string key = _lastReportKey;
        if (_operations.ContainsKey(key)) return;
        var request = _lastReportRequest;
        ReportState = AsyncEchoOperationState.Pending;
        ReportError = null;
        Start(key, request, reply =>
        {
            if (_lastReportKey != key) return;
            if (reply.ok && !string.IsNullOrEmpty(reply.data.receiptId)
                && reply.data.acceptedAt > 0)
            {
                ReportState = AsyncEchoOperationState.Succeeded;
                if (LeaderboardBoardId == (string)request["boardId"])
                    LoadLeaderboard(LeaderboardBoardId, _lastReportRules);
            }
            else
            {
                ReportError = reply.ok ? "INVALID_RESPONSE" : reply.error.code;
                ReportState = ReportError == "TIMEOUT" || ReportError == "NETWORK_ERROR"
                    ? AsyncEchoOperationState.Unconfirmed : FailureState(ReportError);
                Notice?.Invoke(ReportError);
            }
            Changed?.Invoke();
        });
        Changed?.Invoke();
    }

    public void LoadLeaderboard(string boardId, int rulesVersion = 1, int limit = 20)
    {
        if (!AsyncEchoInvitation.IsValidId(boardId) || _disposed) return;
        _operations.Remove("leaderboard");
        int epoch = ++_leaderboardEpoch;
        LeaderboardBoardId = boardId;
        LeaderboardState = AsyncEchoOperationState.Pending;
        LeaderboardError = null;
        Leaderboard = new AsyncEchoLeaderboardEntry[0];
        var request = Request("leaderboard");
        request["boardId"] = boardId;
        request["limit"] = Math.Max(1, Math.Min(50, limit));
        Start("leaderboard", request, reply =>
        {
            if (epoch != _leaderboardEpoch) return;
            if (reply.ok && reply.data.boardId == boardId
                && reply.data.rulesVersion == rulesVersion && ValidLeaderboard(reply.data.items))
            {
                Leaderboard = reply.data.items;
                LeaderboardState = AsyncEchoOperationState.Succeeded;
            }
            else
            {
                LeaderboardError = reply.ok ? "INVALID_RESPONSE" : reply.error.code;
                LeaderboardState = FailureState(LeaderboardError);
            }
            Changed?.Invoke();
        });
        Changed?.Invoke();
    }

    private static bool ValidLeaderboard(AsyncEchoLeaderboardEntry[] items)
    {
        if (items == null || items.Length > 50) return false;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        int rank = 0;
        foreach (var item in items)
            if (item == null || item.rank != ++rank || !AsyncEchoInvitation.IsValidId(item.entryId)
                || !ids.Add(item.entryId)
                || string.IsNullOrEmpty(item.displayLabel) || item.displayLabel.Length > 64
                || double.IsNaN(item.distanceMeters) || double.IsInfinity(item.distanceMeters)
                || item.distanceMeters < 0 || item.distanceMeters > 10000000
                || double.IsNaN(item.playerLeadMeters) || double.IsInfinity(item.playerLeadMeters)
                || Math.Abs(item.playerLeadMeters) > 10000000) return false;
        return true;
    }

    public void Tick()
    {
        if (_disposed) return;
        double now = _clock();
        foreach (Operation op in new List<Operation>(_operations.Values))
        {
            Operation current;
            if (!_operations.TryGetValue(op.key, out current) || current != op) continue;
            if (now >= op.deadline) Finish(op, Error("TIMEOUT"));
            else if (now >= op.attemptDeadline) RetryOrFinish(op, Error("TIMEOUT", true));
        }
        while (true)
        {
            Completion completion;
            lock (_queueLock)
            {
                if (_completions.Count == 0) break;
                completion = _completions.Dequeue();
            }
            Operation active;
            Operation op = completion.operation;
            if (!_operations.TryGetValue(op.key, out active) || active != op
                || op.attempt != completion.attempt) continue;
            AsyncEchoCloudReply reply = Parse(completion.result);
            if (!reply.ok && reply.error.retryable) RetryOrFinish(op, reply);
            else Finish(op, reply);
        }
    }

    private void Start(string key, Dictionary<string, object> request, Action<AsyncEchoCloudReply> completed)
    {
        if (_disposed) return;
        if (!_transport.IsAvailable)
        { completed(Error(_transport.UnavailableReason ?? "ASYNC_UNAVAILABLE")); return; }
        var op = new Operation { key = key, request = new Dictionary<string, object>(request),
            deadline = _clock() + 10d, completed = completed };
        _operations[key] = op;
        Attempt(op);
    }

    private void Attempt(Operation op)
    {
        op.attempt++;
        op.attemptDeadline = Math.Min(op.deadline, _clock() + 5d);
        int attempt = op.attempt;
        Action<AsyncEchoTransportResult> enqueue = result =>
        {
            lock (_queueLock)
                if (!_disposed) _completions.Enqueue(new Completion
                    { operation = op, attempt = attempt, result = result });
        };
        try { _transport.Send(new Dictionary<string, object>(op.request), enqueue); }
        catch (Exception) { enqueue(new AsyncEchoTransportResult(null, "NETWORK_ERROR", true)); }
    }

    private void RetryOrFinish(Operation op, AsyncEchoCloudReply reply)
    {
        if (op.attempt < 2 && _clock() < op.deadline) Attempt(op);
        else Finish(op, reply);
    }
    private void Finish(Operation op, AsyncEchoCloudReply reply)
    {
        Operation current;
        if (!_operations.TryGetValue(op.key, out current) || current != op) return;
        _operations.Remove(op.key); op.completed(reply);
    }

    private static AsyncEchoCloudReply Parse(AsyncEchoTransportResult response)
    {
        if (response == null) return Error("INVALID_RESPONSE");
        if (response.ErrorCode != null) return Error(response.ErrorCode, response.Retryable);
        if (string.IsNullOrEmpty(response.Json) || Encoding.UTF8.GetByteCount(response.Json) > 65536)
            return Error("INVALID_RESPONSE");
        try
        {
            var reply = JsonUtility.FromJson<AsyncEchoCloudReply>(response.Json);
            if (reply == null || reply.apiVersion != 1) return Error("API_UNSUPPORTED");
            if (reply.ok ? reply.data == null : reply.error == null || string.IsNullOrEmpty(reply.error.code))
                return Error("INVALID_RESPONSE");
            return reply;
        }
        catch (ArgumentException) { return Error("INVALID_RESPONSE"); }
    }
    private static AsyncEchoCloudReply Error(string code, bool retryable = false)
        => new AsyncEchoCloudReply { apiVersion = 1, error = new AsyncEchoCloudError
            { code = code, retryable = retryable } };
    private static AsyncEchoOperationState FailureState(string code)
        => code == "ASYNC_UNAVAILABLE" || code == "CLOUD_NOT_CONFIGURED" || code == "SDK_NOT_READY"
            ? AsyncEchoOperationState.Unavailable : AsyncEchoOperationState.Failed;
    private static Dictionary<string, object> Request(string action)
        => new Dictionary<string, object> { { "apiVersion", 1 }, { "action", action } };

    public void Dispose()
    {
        lock (_queueLock) { _disposed = true; _completions.Clear(); }
        _operations.Clear();
        Changed = null; Notice = null;
    }
}

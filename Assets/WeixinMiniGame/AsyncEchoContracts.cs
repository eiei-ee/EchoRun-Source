using System;

public enum AsyncEchoOperationState { Idle, Pending, Succeeded, Failed, Unconfirmed, Unavailable }

// Operation envelopes only; ActiveEchoIdentity remains the only identity model.
[Serializable]
public sealed class AsyncEchoCloudReply
{
    public bool ok;
    public int apiVersion;
    public AsyncEchoCloudData data;
    public AsyncEchoCloudError error;
}
[Serializable]
public sealed class AsyncEchoCloudError { public string code; public bool retryable; }

[Serializable]
public sealed class AsyncEchoCloudData
{
    public bool found;
    public string boardId;
    public string inviterOpenid;
    public string ownerOpenid;
    public string identityId;
    public string payloadJson;
    public int payloadVersion;
    public int generation;
    public int rulesVersion;
    public int runSeed;
    public string receiptId;
    public long acceptedAt;
    public bool duplicate;
    public bool eligible;
    public AsyncEchoLeaderboardEntry[] items;
}
[Serializable]
public sealed class AsyncEchoLeaderboardEntry
{
    public int rank;
    public string entryId;
    public string displayLabel;
    public bool isMe;
    public double distanceMeters;
    public double playerLeadMeters;
    public bool playerWon;
}

public sealed class AsyncEchoSnapshot
{
    private readonly ActiveEchoIdentity _identity;
    public string BoardId { get; }
    public AsyncEchoInvitation Invitation { get; }
    public int RunSeed { get; }
    public int Generation => _identity.generation;
    public ActiveEchoIdentity CreateOpponent() => _identity.Clone();
    public AsyncEchoSnapshot(string boardId, AsyncEchoInvitation invitation,
        int runSeed, ActiveEchoIdentity identity)
    {
        BoardId = boardId; Invitation = invitation; RunSeed = runSeed;
        _identity = identity.Clone();
    }
}
public sealed class AsyncEchoPublishReceipt
{
    public string BoardId { get; }
    public AsyncEchoInvitation Invitation { get; }
    public AsyncEchoPublishReceipt(string boardId, AsyncEchoInvitation invitation)
    { BoardId = boardId; Invitation = invitation; }
}

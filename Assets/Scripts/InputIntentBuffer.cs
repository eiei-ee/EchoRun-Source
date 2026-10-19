using System;
using System.Collections.Generic;

public enum InputIntentSource
{
    Keyboard,
    Touch,
    Mouse,
    Replay
}

public enum InputIntentOutcome
{
    Pending,
    Executed,
    Rejected,
    Expired,
    Dropped
}

[Serializable]
public struct BufferedSwipeCommand
{
    public int sequence;
    public SwipeDirection direction;
    public InputIntentSource source;
    public float issuedAt;
    public float expiresAt;

    public BufferedSwipeCommand(int sequence, SwipeDirection direction,
        InputIntentSource source, float issuedAt, float expiresAt)
    {
        this.sequence = sequence;
        this.direction = direction;
        this.source = source;
        this.issuedAt = issuedAt;
        this.expiresAt = expiresAt;
    }
}

public sealed class InputIntentBuffer
{
    public const float HorizontalLifetime = 0.08f;
    public const float VerticalLifetime = 0.12f;
    public const int Capacity = 4;

    private readonly Queue<BufferedSwipeCommand> _commands =
        new Queue<BufferedSwipeCommand>(Capacity);
    private int _nextSequence = 1;

    public int Count => _commands.Count;

    public BufferedSwipeCommand Enqueue(SwipeDirection direction,
        InputIntentSource source, float issuedAt,
        out BufferedSwipeCommand evicted)
    {
        evicted = default;
        if (_commands.Count >= Capacity)
            evicted = _commands.Dequeue();

        float lifetime = IsVertical(direction)
            ? VerticalLifetime
            : HorizontalLifetime;
        var command = new BufferedSwipeCommand(
            NextSequence(), direction, source, issuedAt,
            issuedAt + lifetime);
        _commands.Enqueue(command);
        return command;
    }

    public bool TryPeek(out BufferedSwipeCommand command)
    {
        if (_commands.Count == 0)
        {
            command = default;
            return false;
        }
        command = _commands.Peek();
        return true;
    }

    public bool TryPopExpired(float now, out BufferedSwipeCommand command)
    {
        if (_commands.Count == 0 || now <= _commands.Peek().expiresAt)
        {
            command = default;
            return false;
        }
        command = _commands.Dequeue();
        return true;
    }

    public bool TryResolveHead(int sequence,
        out BufferedSwipeCommand command)
    {
        if (_commands.Count == 0 || _commands.Peek().sequence != sequence)
        {
            command = default;
            return false;
        }
        command = _commands.Dequeue();
        return true;
    }

    public bool TryDeferHead(int sequence)
    {
        if (_commands.Count == 0 || _commands.Peek().sequence != sequence)
            return false;
        BufferedSwipeCommand command = _commands.Dequeue();
        _commands.Enqueue(command);
        return true;
    }

    public bool TryDequeue(out BufferedSwipeCommand command)
    {
        if (_commands.Count == 0)
        {
            command = default;
            return false;
        }
        command = _commands.Dequeue();
        return true;
    }

    public void Clear()
    {
        _commands.Clear();
    }

    public static bool IsVertical(SwipeDirection direction)
    {
        return direction == SwipeDirection.Up
               || direction == SwipeDirection.Down;
    }

    private int NextSequence()
    {
        int result = _nextSequence++;
        if (_nextSequence <= 0) _nextSequence = 1;
        return result;
    }
}

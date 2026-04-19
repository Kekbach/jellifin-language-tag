using System;

namespace Jellyfin.Plugin.LanguageFlags.Services;

public sealed class RunStatusStore
{
    public static RunStatusStore Instance { get; } = new();

    private readonly object _sync = new();

    public bool IsRunning { get; private set; }
    public int Processed { get; private set; }
    public int Total { get; private set; }
    public string? CurrentItem { get; private set; }
    public string? Message { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public bool IsCompleted { get; private set; }
    public bool IsFailed { get; private set; }

    private RunStatusStore()
    {
    }

    public void Start(int total)
    {
        lock (_sync)
        {
            IsRunning = true;
            IsCompleted = false;
            IsFailed = false;
            Processed = 0;
            Total = total;
            CurrentItem = null;
            Message = "Starting";
            UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    public void UpdateProgress(int processed, int total, string? currentItem)
    {
        lock (_sync)
        {
            IsRunning = true;
            IsCompleted = false;
            IsFailed = false;
            Processed = processed;
            Total = total;
            CurrentItem = currentItem;
            Message = "Processing";
            UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    public void Complete(string? message = null)
    {
        lock (_sync)
        {
            IsRunning = false;
            IsCompleted = true;
            IsFailed = false;
            Message = message ?? "Finished";
            CurrentItem = null;
            UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    public void Fail(string? message = null)
    {
        lock (_sync)
        {
            IsRunning = false;
            IsCompleted = true;
            IsFailed = true;
            Message = message ?? "Failed";
            CurrentItem = null;
            UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    public object Snapshot()
    {
        lock (_sync)
        {
            return new
            {
                isRunning = IsRunning,
                isCompleted = IsCompleted,
                isFailed = IsFailed,
                processed = Processed,
                total = Total,
                currentItem = CurrentItem,
                message = Message,
                updatedAt = UpdatedAt
            };
        }
    }
}
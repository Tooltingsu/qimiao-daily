namespace QimiaoDaily.Data;

public sealed class ProviderHealthRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ProviderName { get; set; } = string.Empty;
    public string Status { get; set; } = "WARNING";
    public DateTimeOffset? LastSuccessAt { get; set; }
    public DateTimeOffset? LastFailureAt { get; set; }
    public long LastLatencyMs { get; set; }
    public int ItemCount { get; set; }
    public string ParserStatus { get; set; } = "UNKNOWN";
    public int FailureCount { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class SchedulerTaskRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TaskKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ScheduleText { get; set; } = string.Empty;
    public string Status { get; set; } = "IDLE";
    public DateTimeOffset? LastRunAt { get; set; }
    public DateTimeOffset? NextRunAt { get; set; }
    public int FailureCount { get; set; }
    public int MaxRetries { get; set; } = 3;
    public string? LastError { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

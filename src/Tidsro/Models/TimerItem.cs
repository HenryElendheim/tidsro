namespace Tidsro.Models;

public sealed class TimerItem
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string? Label { get; set; }
    public TriggerType TriggerType { get; init; } = TriggerType.Countdown;
    public SoundChoice Sound { get; set; } = SoundChoice.None;

    // Countdown runtime (Slice 1)
    public TimeSpan OriginalDuration { get; set; }
    public TimeSpan? Duration { get; set; }
    public DateTimeOffset? EndsAt { get; set; }
    public TimeSpan? PausedRemaining { get; set; }
    public TimerState State { get; set; } = TimerState.Idle;
}

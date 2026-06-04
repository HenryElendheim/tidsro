namespace Tidsro.Models;

public sealed class AppSettings
{
    public int SchemaVersion { get; set; } = 1;
    public bool LaunchAtStartup { get; set; }
    public SoundChoice DefaultSound { get; set; } = SoundChoice.None;

    public static AppSettings Defaults() => new();

    /// <summary>Harden untrusted input loaded from disk: unknown enum -> None.</summary>
    public AppSettings Sanitized() => new()
    {
        SchemaVersion = 1,
        LaunchAtStartup = LaunchAtStartup,
        DefaultSound = Enum.IsDefined(DefaultSound) ? DefaultSound : SoundChoice.None,
    };
}

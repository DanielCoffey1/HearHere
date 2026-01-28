namespace HearHere.Audio;

/// <summary>Represents one audio playback endpoint.</summary>
public sealed class AudioDevice
{
    /// <summary>MMDevice endpoint ID string (e.g. {0.0.0.00000000}.{guid}).</summary>
    public string Id { get; init; } = "";

    /// <summary>Human-readable name (e.g. "Speakers (Realtek Audio)").</summary>
    public string FriendlyName { get; init; } = "";

    /// <summary>Whether this is the current default playback device.</summary>
    public bool IsDefault { get; set; }

    public override string ToString() => FriendlyName;
}

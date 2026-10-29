namespace PhoneAudioBluetooth.Models;

public sealed class AudioDeviceItem
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public bool IsPaired { get; init; }

    public bool IsAvailable { get; set; }

    public string DisplayName =>
        string.IsNullOrWhiteSpace(Name) ? "未命名设备" : Name;
}

using System.Collections.ObjectModel;

namespace PhoneAudioBluetooth.Infrastructure;

public sealed class AppLogger
{
    private const int MaximumEntries = 100;
    private readonly ObservableCollection<string> _entries = new();

    public ReadOnlyObservableCollection<string> Entries { get; }

    public AppLogger()
    {
        Entries = new ReadOnlyObservableCollection<string>(_entries);
    }

    public void Info(string message)
    {
        Add("信息", message);
    }

    public void Error(string message)
    {
        Add("错误", message);
    }

    private void Add(string level, string message)
    {
        var entry = $"[{DateTime.Now:HH:mm:ss}] [{level}] {message}";
        _entries.Insert(0, entry);

        while (_entries.Count > MaximumEntries)
        {
            _entries.RemoveAt(_entries.Count - 1);
        }
    }
}

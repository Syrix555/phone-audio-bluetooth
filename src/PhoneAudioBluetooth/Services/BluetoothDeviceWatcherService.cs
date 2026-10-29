using System.Collections.ObjectModel;
using Windows.Devices.Enumeration;
using Windows.Media.Audio;
using PhoneAudioBluetooth.Models;

namespace PhoneAudioBluetooth.Services;

public sealed class BluetoothDeviceWatcherService : IDisposable
{
    private readonly Dictionary<string, AudioDeviceItem> _devices = new();
    private DeviceWatcher? _watcher;
    private bool _disposed;

    public event EventHandler? DevicesChanged;

    public IReadOnlyList<AudioDeviceItem> Devices =>
        new ReadOnlyCollection<AudioDeviceItem>(_devices.Values
            .OrderBy(device => device.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList());

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        StopWatcher();
        _devices.Clear();

        var selector = AudioPlaybackConnection.GetDeviceSelector();
        _watcher = DeviceInformation.CreateWatcher(selector);
        _watcher.Added += OnDeviceAdded;
        _watcher.Updated += OnDeviceUpdated;
        _watcher.Removed += OnDeviceRemoved;
        _watcher.EnumerationCompleted += OnEnumerationCompleted;
        _watcher.Start();
    }

    public Task RefreshAsync()
    {
        Start();
        return Task.CompletedTask;
    }

    private void OnDeviceAdded(DeviceWatcher sender, DeviceInformation args)
    {
        if (string.IsNullOrWhiteSpace(args.Id)
            || args.Pairing is null
            || !args.Pairing.IsPaired)
        {
            return;
        }

        _devices[args.Id] = CreateDeviceItem(args);
        RaiseDevicesChanged();
    }

    private void OnDeviceUpdated(DeviceWatcher sender, DeviceInformationUpdate args)
    {
        if (_devices.TryGetValue(args.Id, out var existing))
        {
            existing.IsAvailable = true;
            RaiseDevicesChanged();
        }
    }

    private void OnDeviceRemoved(DeviceWatcher sender, DeviceInformationUpdate args)
    {
        if (_devices.Remove(args.Id))
        {
            RaiseDevicesChanged();
        }
    }

    private void OnEnumerationCompleted(DeviceWatcher sender, object args)
    {
        RaiseDevicesChanged();
    }

    private static AudioDeviceItem CreateDeviceItem(DeviceInformation information)
    {
        return new AudioDeviceItem
        {
            Id = information.Id,
            Name = information.Name ?? string.Empty,
            IsPaired = information.Pairing?.IsPaired ?? false,
            IsAvailable = true
        };
    }

    private void RaiseDevicesChanged()
    {
        DevicesChanged?.Invoke(this, EventArgs.Empty);
    }

    private void StopWatcher()
    {
        if (_watcher is null)
        {
            return;
        }

        _watcher.Added -= OnDeviceAdded;
        _watcher.Updated -= OnDeviceUpdated;
        _watcher.Removed -= OnDeviceRemoved;
        _watcher.EnumerationCompleted -= OnEnumerationCompleted;

        if (_watcher.Status is not DeviceWatcherStatus.Stopped
            and not DeviceWatcherStatus.Created
            and not DeviceWatcherStatus.Aborted)
        {
            _watcher.Stop();
        }

        _watcher = null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StopWatcher();
        _devices.Clear();
        DevicesChanged = null;
    }
}

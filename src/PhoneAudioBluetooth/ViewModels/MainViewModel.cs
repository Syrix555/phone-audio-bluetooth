using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Dispatching;
using Windows.Media.Audio;
using PhoneAudioBluetooth.Infrastructure;
using PhoneAudioBluetooth.Models;
using PhoneAudioBluetooth.Services;

namespace PhoneAudioBluetooth.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged, IAsyncDisposable
{
    private readonly BluetoothDeviceWatcherService _deviceWatcher = new();
    private readonly AudioPlaybackConnectionService _audioService = new();
    private readonly AppLogger _logger = new();
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private readonly DispatcherQueue? _dispatcherQueue;
    private AudioDeviceItem? _selectedDevice;
    private ConnectionViewState _connectionState = ConnectionViewState.NoDevice;
    private bool _isInitialized;
    private bool _disposed;

    public MainViewModel(DispatcherQueue? dispatcherQueue = null)
    {
        _dispatcherQueue = dispatcherQueue;
        _deviceWatcher.DevicesChanged += OnDevicesChanged;
        _audioService.StateChanged += OnAudioStateChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<AudioDeviceItem> Devices { get; } = new();

    public ReadOnlyObservableCollection<string> Logs => _logger.Entries;

    public AudioDeviceItem? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            if (ReferenceEquals(_selectedDevice, value))
            {
                return;
            }

            _selectedDevice = value;
            if (value is not null
                && (ConnectionState is
                    ConnectionViewState.NoDevice
                    or ConnectionViewState.Disconnected
                    or ConnectionViewState.Failed))
            {
                ConnectionState = ConnectionViewState.DeviceSelected;
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(CanConnect));
        }
    }

    public ConnectionViewState ConnectionState
    {
        get => _connectionState;
        private set
        {
            if (_connectionState == value)
            {
                return;
            }

            _connectionState = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(CanConnect));
            OnPropertyChanged(nameof(CanDisconnect));
        }
    }

    public string StatusText => ConnectionState switch
    {
        ConnectionViewState.NoDevice => "状态：未选择设备",
        ConnectionViewState.DeviceSelected => "状态：已选择设备",
        ConnectionViewState.Starting => "状态：正在启动音频接收",
        ConnectionViewState.Connecting => "状态：正在连接手机",
        ConnectionViewState.Connected => "状态：已连接",
        ConnectionViewState.Disconnecting => "状态：正在断开",
        ConnectionViewState.Disconnected => "状态：已断开",
        ConnectionViewState.Failed => "状态：连接失败",
        _ => "状态：未知"
    };

    public bool CanConnect =>
        SelectedDevice is not null
        && (ConnectionState is
            ConnectionViewState.DeviceSelected
            or ConnectionViewState.Disconnected
            or ConnectionViewState.Failed);

    public bool CanDisconnect =>
        ConnectionState is
            ConnectionViewState.Starting
            or ConnectionViewState.Connecting
            or ConnectionViewState.Connected;

    public async Task InitializeAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;
        try
        {
            _logger.Info("启动设备发现。");
            _deviceWatcher.Start();
        }
        catch (Exception exception)
        {
            _logger.Error($"启动设备发现失败：{exception.Message}");
        }

        await Task.CompletedTask;
    }

    public async Task RefreshDevicesAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _operationGate.WaitAsync();
        try
        {
            _logger.Info("刷新蓝牙音频设备。");
            await _deviceWatcher.RefreshAsync();
        }
        catch (Exception exception)
        {
            _logger.Error($"刷新设备失败：{exception.Message}");
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task ConnectAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (SelectedDevice is null)
        {
            _logger.Error("连接前没有选择设备。");
            return;
        }

        await _operationGate.WaitAsync();
        try
        {
            var device = SelectedDevice;
            ConnectionState = ConnectionViewState.Starting;
            _logger.Info($"准备连接设备：{device.DisplayName}");

            var startResult = await _audioService.StartAsync(device.Id);
            if (!startResult.Success)
            {
                SetConnectionFailure($"启动音频接收失败：{startResult.ErrorMessage}");
                return;
            }

            ConnectionState = ConnectionViewState.Connecting;
            _logger.Info("StartAsync 成功，正在打开音频连接。");

            var openResult = await _audioService.OpenAsync();
            if (!openResult.Success)
            {
                await CloseAfterConnectionFailureAsync();
                SetConnectionFailure($"打开音频连接失败：{openResult.ErrorMessage}");
                return;
            }

            ConnectionState = ConnectionViewState.Connected;
            _logger.Info("手机音频连接已建立。");
        }
        catch (Exception exception)
        {
            await CloseAfterConnectionFailureAsync();
            SetConnectionFailure($"连接过程中发生异常：{exception.Message}");
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task DisconnectAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _operationGate.WaitAsync();
        try
        {
            ConnectionState = ConnectionViewState.Disconnecting;
            _logger.Info("正在断开手机音频连接。");
            await _audioService.CloseAsync();
            ConnectionState = ConnectionViewState.Disconnected;
            _logger.Info("手机音频连接已断开。");
        }
        catch (Exception exception)
        {
            SetConnectionFailure($"断开连接失败：{exception.Message}");
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private void OnDevicesChanged(object? sender, EventArgs args)
    {
        RunOnUiThread(() =>
        {
            var selectedId = SelectedDevice?.Id;
            Devices.Clear();

            foreach (var device in _deviceWatcher.Devices)
            {
                Devices.Add(device);
            }

            SelectedDevice = selectedId is null
                ? Devices.FirstOrDefault()
                : Devices.FirstOrDefault(device => device.Id == selectedId)
                    ?? Devices.FirstOrDefault();

            if (Devices.Count == 0
                && (ConnectionState is
                    ConnectionViewState.NoDevice
                    or ConnectionViewState.DeviceSelected))
            {
                ConnectionState = ConnectionViewState.NoDevice;
                _logger.Info("没有发现可用的蓝牙音频设备。");
            }
        });
    }

    private void OnAudioStateChanged(
        object? sender,
        AudioConnectionStateChangedEventArgs args)
    {
        RunOnUiThread(() =>
        {
            _logger.Info($"AudioPlaybackConnection 状态：{args.State}");
            if (args.State == AudioPlaybackConnectionState.Closed)
            {
                if (ConnectionState is not ConnectionViewState.Disconnecting)
                {
                    ConnectionState = ConnectionViewState.Disconnected;
                }

                return;
            }

            if (args.State == AudioPlaybackConnectionState.Opened)
            {
                ConnectionState = ConnectionViewState.Connected;
            }
        });
    }

    private void SetConnectionFailure(string message)
    {
        ConnectionState = ConnectionViewState.Failed;
        _logger.Error(message);
    }

    private async Task CloseAfterConnectionFailureAsync()
    {
        try
        {
            await _audioService.CloseAsync();
        }
        catch (Exception exception)
        {
            _logger.Error($"清理失败连接时发生异常：{exception.Message}");
        }
    }

    private void RunOnUiThread(Action action)
    {
        if (_dispatcherQueue is null || _dispatcherQueue.HasThreadAccess)
        {
            action();
            return;
        }

        _dispatcherQueue.TryEnqueue(() => action());
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(propertyName));
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _deviceWatcher.DevicesChanged -= OnDevicesChanged;
        _audioService.StateChanged -= OnAudioStateChanged;

        await _audioService.DisposeAsync();
        _deviceWatcher.Dispose();
        _operationGate.Dispose();
    }
}

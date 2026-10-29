using Windows.Media.Audio;

namespace PhoneAudioBluetooth.Services;

public interface IAudioPlaybackConnectionService : IAsyncDisposable
{
    bool IsStarted { get; }

    bool IsOpen { get; }

    string? CurrentDeviceId { get; }

    event EventHandler<AudioConnectionStateChangedEventArgs>? StateChanged;

    Task<StartConnectionResult> StartAsync(
        string deviceId,
        CancellationToken cancellationToken = default);

    Task<OpenConnectionResult> OpenAsync(
        CancellationToken cancellationToken = default);

    Task CloseAsync();
}

public sealed class AudioConnectionStateChangedEventArgs : EventArgs
{
    public AudioConnectionStateChangedEventArgs(
        string? deviceId,
        AudioPlaybackConnectionState state)
    {
        DeviceId = deviceId;
        State = state;
    }

    public string? DeviceId { get; }

    public AudioPlaybackConnectionState State { get; }
}

public sealed record StartConnectionResult(
    bool Success,
    string? ErrorMessage = null);

public sealed record OpenConnectionResult(
    bool Success,
    AudioPlaybackConnectionOpenResultStatus? Status = null,
    string? ErrorMessage = null);

public sealed class AudioPlaybackConnectionService : IAudioPlaybackConnectionService
{
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private AudioPlaybackConnection? _connection;
    private bool _disposed;

    public bool IsStarted { get; private set; }

    public bool IsOpen { get; private set; }

    public string? CurrentDeviceId { get; private set; }

    public event EventHandler<AudioConnectionStateChangedEventArgs>? StateChanged;

    public async Task<StartConnectionResult> StartAsync(
        string deviceId,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        cancellationToken.ThrowIfCancellationRequested();

        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            await CloseCoreAsync();

            var connection = AudioPlaybackConnection.TryCreateFromId(deviceId);
            if (connection is null)
            {
                return new StartConnectionResult(
                    false,
                    "无法创建 AudioPlaybackConnection。请确认设备仍已配对。");
            }

            _connection = connection;
            CurrentDeviceId = deviceId;
            _connection.StateChanged += OnConnectionStateChanged;

            try
            {
                await _connection.StartAsync();
                IsStarted = true;
                return new StartConnectionResult(true);
            }
            catch (Exception exception)
            {
                await CloseCoreAsync();
                return new StartConnectionResult(false, exception.Message);
            }
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task<OpenConnectionResult> OpenAsync(
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            if (_connection is null || !IsStarted)
            {
                return new OpenConnectionResult(
                    false,
                    ErrorMessage: "音频接收能力尚未启动。");
            }

            try
            {
                var result = await _connection.OpenAsync();
                IsOpen = result.Status == AudioPlaybackConnectionOpenResultStatus.Success;

                if (IsOpen)
                {
                    return new OpenConnectionResult(true, result.Status);
                }

                return new OpenConnectionResult(
                    false,
                    result.Status,
                    $"OpenAsync 返回状态：{result.Status}");
            }
            catch (Exception exception)
            {
                IsOpen = false;
                return new OpenConnectionResult(false, ErrorMessage: exception.Message);
            }
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task CloseAsync()
    {
        if (_disposed)
        {
            return;
        }

        await _operationGate.WaitAsync();
        try
        {
            await CloseCoreAsync();
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private Task CloseCoreAsync()
    {
        if (_connection is not null)
        {
            _connection.StateChanged -= OnConnectionStateChanged;
            _connection.Close();
            _connection.Dispose();
        }

        _connection = null;
        CurrentDeviceId = null;
        IsStarted = false;
        IsOpen = false;
        return Task.CompletedTask;
    }

    private void OnConnectionStateChanged(
        AudioPlaybackConnection sender,
        object args)
    {
        StateChanged?.Invoke(
            this,
            new AudioConnectionStateChangedEventArgs(
                CurrentDeviceId,
                sender.State));
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await CloseAsync();
        _disposed = true;
        StateChanged = null;
        _operationGate.Dispose();
    }
}

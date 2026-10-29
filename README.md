# PhoneAudioBluetooth

这是“手机蓝牙音频接收器”的第一版 Windows 实现。程序使用
`AudioPlaybackConnection` 接收已经通过 Windows 蓝牙设置配对的手机媒体音频，
并让 Windows 把它输出到当前默认音频设备。

当前代码覆盖第一版的基础链路：

- WinUI 3 最小窗口；
- `AudioPlaybackConnection.GetDeviceSelector()` + `DeviceWatcher` 发现设备；
- 选择一个已配对设备；
- `StartAsync()` 和 `OpenAsync()` 建立连接；
- `StateChanged` 状态监听；
- 手动断开；
- 连接对象、设备观察器和事件订阅的释放；
- 简单的界面日志。

当前没有实现托盘、开机启动、自动重连、独立音量和虚拟声卡。

## Windows 上构建

在 Windows 双系统中使用 Visual Studio 2022，并安装：

- .NET 8 SDK；
- Windows 10/11 SDK；
- Windows App SDK 对应的 C# 工作负载。

打开 `PhoneAudioBluetooth.sln`，选择 `x64`，然后生成并运行。
首次还需要在 Windows 蓝牙设置中把手机与电脑配对。

本目录是在 Linux 环境下编写的，当前没有执行 Windows/.NET 实际编译。

## 手动验证顺序

1. 将有线耳机设置为 Windows 默认输出设备。
2. 在 Windows 蓝牙设置中完成手机配对。
3. 启动程序并等待设备列表出现手机。
4. 选择手机，点击“连接手机音频”。
5. 在手机上播放视频或音乐，确认声音进入有线耳机。
6. 同时播放电脑游戏或其他音频，确认两路声音可以并行输出。
7. 点击“断开”，确认手机声音停止且电脑声音继续。

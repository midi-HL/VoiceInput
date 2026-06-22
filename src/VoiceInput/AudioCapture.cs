using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace VoiceInput
{
    public class AudioCapture : IDisposable
    {
        #region WASAPI COM 接口（使用正确的 vtable 顺序）

        // MMDeviceEnumerator 的 CLSID
        private static readonly Guid CLSID_MMDeviceEnumerator = new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E");

        // IMMDeviceEnumerator 的 IID
        private static readonly Guid IID_IMMDeviceEnumerator = new Guid("A95664D2-9614-4F35-A746-DE8DB63617E6");

        // IAudioClient 的 IID
        private static readonly Guid IID_IAudioClient = new Guid("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2");

        // IAudioCaptureClient 的 IID
        private static readonly Guid IID_IAudioCaptureClient = new Guid("C8ADBD64-E71E-48A0-A4DE-185C395CD317");

        // EDataFlow 枚举
        private const int eRender = 0;
        private const int eCapture = 1;

        // ERole 枚举
        private const int eConsole = 0;

        // CLSCTX
        private const int CLSCTX_ALL = 23;

        // AUDCLNT_SHAREMODE
        private const int AUDCLNT_SHAREMODE_SHARED = 0;

        // WAVEFORMATEX
        private const int WAVE_FORMAT_PCM = 1;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WAVEFORMATEX
        {
            public ushort wFormatTag;
            public ushort nChannels;
            public uint nSamplesPerSec;
            public uint nAvgBytesPerSec;
            public ushort nBlockAlign;
            public ushort wBitsPerSample;
            public ushort cbSize;
        }

        #endregion

        #region COM 接口定义（使用 ComImport）

        [ComImport]
        [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            void EnumAudioEndpoints(
                [In] int dataFlow,
                [In] int dwStateMask,
                [MarshalAs(UnmanagedType.Interface)] out object ppDevices);

            void GetDefaultAudioEndpoint(
                [In] int dataFlow,
                [In] int role,
                [MarshalAs(UnmanagedType.Interface)] out IMMDevice ppEndpoint);

            void GetDevice(
                [In] [MarshalAs(UnmanagedType.LPWStr)] string pwstrId,
                [MarshalAs(UnmanagedType.Interface)] out IMMDevice ppDevice);

            void RegisterEndpointNotificationCallback(
                [In] [MarshalAs(UnmanagedType.Interface)] object pClient);

            void UnregisterEndpointNotificationCallback(
                [In] [MarshalAs(UnmanagedType.Interface)] object pClient);
        }

        [ComImport]
        [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            void Activate(
                [In] ref Guid iid,
                [In] int dwClsCtx,
                [In] IntPtr pActivationParams,
                [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);

            void OpenPropertyStore(
                [In] int stgmAccess,
                [MarshalAs(UnmanagedType.Interface)] out object ppProperties);

            void GetId(
                [MarshalAs(UnmanagedType.LPWStr)] out string ppstrId);

            void GetState(out int pdwState);
        }

        [ComImport]
        [Guid("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioClient
        {
            void Initialize(
                [In] int streamFlags,
                [In] long bufferDuration,
                [In] long periodicity,
                [In] ref WAVEFORMATEX pFormat,
                [In] ref Guid audioSessionGuid);

            void GetBufferSize(out uint pNumBufferFrames);

            void GetStreamLatency(out long phnsLatency);

            void GetCurrentPadding(out uint pNumPaddingFrames);

            void IsFormatSupported(
                [In] int shareMode,
                [In] ref WAVEFORMATEX pFormat,
                out WAVEFORMATEX ppClosestMatch);

            void GetMixFormat(out IntPtr ppDeviceFormat);

            void GetDevicePeriod(
                out long phnsDefaultDevicePeriod,
                out long phnsMinimumDevicePeriod);

            void Start();

            void Stop();

            void Reset();

            void SetEventHandle([In] IntPtr eventHandle);

            void GetService(
                [In] ref Guid riid,
                [MarshalAs(UnmanagedType.IUnknown)] out object ppv);
        }

        [ComImport]
        [Guid("C8ADBD64-E71E-48A0-A4DE-185C395CD317")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioCaptureClient
        {
            void GetBuffer(
                out IntPtr ppData,
                out uint pNumFramesToRead,
                out uint pdwFlags,
                out long pu64DevicePosition,
                out long pu64QPCPosition);

            void ReleaseBuffer([In] uint numFramesRead);

            void GetNextPacketSize(out uint pNumFramesInNextPacket);
        }

        #endregion

        private const int SampleRate = 16000;
        private const int Channels = 1;
        private const int BitsPerSample = 16;

        private IMMDeviceEnumerator? _enumerator;
        private IMMDevice? _device;
        private IAudioClient? _audioClient;
        private IAudioCaptureClient? _captureClient;

        private Thread? _captureThread;
        private CancellationTokenSource? _cts;
        private bool _isCapturing;

        // 音频数据缓冲区
        private readonly ConcurrentQueue<byte[]> _audioBuffers = new();

        // RMS 电平计算
        private double _currentRmsLevel;
        private readonly double[] _rmsHistory = new double[60];
        private int _rmsHistoryIndex;

        // 事件
        public event EventHandler<double>? RmsLevelChanged;
        public event EventHandler<byte[]>? AudioBufferReady;

        // 状态属性
        public bool IsCapturing => _isCapturing;
        public double CurrentRmsLevel => _currentRmsLevel;

        public AudioCapture()
        {
            InitializeAudioClient();
        }

        private void InitializeAudioClient()
        {
            try
            {
                // 创建设备枚举器
                Logger.Info("AudioCapture: 创建设备枚举器...");
                Type enumeratorType = Type.GetTypeFromCLSID(CLSID_MMDeviceEnumerator)!;
                _enumerator = (IMMDeviceEnumerator)Activator.CreateInstance(enumeratorType)!;

                // 获取默认麦克风（捕获端点）
                Logger.Info("AudioCapture: 获取默认麦克风...");
                try
                {
                    _enumerator.GetDefaultAudioEndpoint(eCapture, eConsole, out _device);
                }
                catch (COMException ex)
                {
                    Logger.Error($"AudioCapture: 获取默认麦克风失败 (HRESULT: 0x{ex.ErrorCode:X8})", ex);
                    throw new Exception("未找到麦克风设备。请检查：\n1. 麦克风是否已连接\n2. 麦克风是否已启用\n3. 应用是否有麦克风权限", ex);
                }

                // 激活音频客户端
                Logger.Info("AudioCapture: 激活音频客户端...");
                try
                {
                    Guid iidAudioClient = IID_IAudioClient;
                    _device.Activate(ref iidAudioClient, CLSCTX_ALL, IntPtr.Zero, out object objAudioClient);
                    _audioClient = (IAudioClient)objAudioClient;
                }
                catch (COMException ex)
                {
                    Logger.Error($"AudioCapture: 激活音频客户端失败 (HRESULT: 0x{ex.ErrorCode:X8})", ex);
                    throw new Exception($"激活音频客户端失败 (HRESULT: 0x{ex.ErrorCode:X8})", ex);
                }

                // 设置音频格式：16kHz, 单声道, 16bit PCM
                var format = new WAVEFORMATEX
                {
                    wFormatTag = WAVE_FORMAT_PCM,
                    nChannels = Channels,
                    nSamplesPerSec = SampleRate,
                    wBitsPerSample = BitsPerSample,
                    nBlockAlign = (ushort)(Channels * BitsPerSample / 8),
                    nAvgBytesPerSec = (uint)(SampleRate * Channels * BitsPerSample / 8),
                    cbSize = 0
                };

                // 初始化音频客户端
                Logger.Info("AudioCapture: 初始化音频客户端...");
                try
                {
                    Guid audioSessionGuid = Guid.Empty;
                    _audioClient.Initialize(
                        AUDCLNT_SHAREMODE_SHARED,
                        0, // 默认缓冲区时长
                        0, // 忽略 periodicity
                        ref format,
                        ref audioSessionGuid);
                }
                catch (COMException ex)
                {
                    Logger.Error($"AudioCapture: 初始化音频客户端失败 (HRESULT: 0x{ex.ErrorCode:X8})", ex);
                    throw new Exception($"初始化音频客户端失败 (HRESULT: 0x{ex.ErrorCode:X8})", ex);
                }

                // 获取捕获客户端
                Logger.Info("AudioCapture: 获取捕获客户端...");
                try
                {
                    Guid iidCaptureClient = IID_IAudioCaptureClient;
                    _audioClient.GetService(ref iidCaptureClient, out object objCaptureClient);
                    _captureClient = (IAudioCaptureClient)objCaptureClient;
                }
                catch (COMException ex)
                {
                    Logger.Error($"AudioCapture: 获取捕获客户端失败 (HRESULT: 0x{ex.ErrorCode:X8})", ex);
                    throw new Exception($"获取捕获客户端失败 (HRESULT: 0x{ex.ErrorCode:X8})", ex);
                }

                Logger.Info("AudioCapture: 初始化完成");
            }
            catch (Exception ex) when (ex is not Exception { InnerException: not null })
            {
                Logger.Error("AudioCapture: 初始化失败", ex);
                throw;
            }
        }

        public void StartCapture()
        {
            if (_isCapturing) return;

            _cts = new CancellationTokenSource();
            _isCapturing = true;

            // 清空缓冲区
            while (_audioBuffers.TryDequeue(out _)) { }

            // 启动音频客户端
            _audioClient?.Start();

            // 启动捕获线程
            _captureThread = new Thread(CaptureLoop)
            {
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal
            };
            _captureThread.Start(_cts.Token);
        }

        public void StopCapture()
        {
            if (!_isCapturing) return;

            _isCapturing = false;
            _cts?.Cancel();

            // 停止音频客户端
            _audioClient?.Stop();

            // 等待捕获线程结束
            _captureThread?.Join(1000);
            _captureThread = null;

            _cts?.Dispose();
            _cts = null;
        }

        private void CaptureLoop(object? state)
        {
            var cancellationToken = (CancellationToken)state!;
            var buffer = new byte[0];

            while (!cancellationToken.IsCancellationRequested && _isCapturing)
            {
                try
                {
                    if (_captureClient == null) break;

                    // 检查是否有数据包
                    _captureClient.GetNextPacketSize(out uint packetLength);
                    if (packetLength == 0)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    // 获取缓冲区
                    _captureClient.GetBuffer(out IntPtr dataPtr, out uint framesAvailable, out uint flags, out _, out _);

                    try
                    {
                        // 计算数据大小
                        int bytesPerFrame = Channels * BitsPerSample / 8;
                        int dataSize = (int)(framesAvailable * bytesPerFrame);

                        // 复制数据
                        if (buffer.Length < dataSize)
                        {
                            buffer = new byte[dataSize];
                        }
                        Marshal.Copy(dataPtr, buffer, 0, dataSize);

                        // 计算 RMS 电平
                        double rms = CalculateRmsLevel(buffer, dataSize);
                        UpdateRmsLevel(rms);

                        // 存储音频数据
                        var audioData = new byte[dataSize];
                        Array.Copy(buffer, audioData, dataSize);
                        _audioBuffers.Enqueue(audioData);

                        // 触发事件
                        AudioBufferReady?.Invoke(this, audioData);
                    }
                    finally
                    {
                        _captureClient.ReleaseBuffer(framesAvailable);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("音频捕获错误", ex);
                    Thread.Sleep(10);
                }
            }
        }

        private double CalculateRmsLevel(byte[] buffer, int dataSize)
        {
            if (dataSize == 0) return 0;

            long sumSquares = 0;
            int sampleCount = dataSize / 2;

            for (int i = 0; i < dataSize - 1; i += 2)
            {
                short sample = (short)(buffer[i] | (buffer[i + 1] << 8));
                sumSquares += (long)sample * sample;
            }

            double rms = Math.Sqrt((double)sumSquares / sampleCount);
            return rms / 32768.0;
        }

        private void UpdateRmsLevel(double rms)
        {
            double attack = 0.4;
            double release = 0.15;

            if (rms > _currentRmsLevel)
            {
                _currentRmsLevel = _currentRmsLevel + (rms - _currentRmsLevel) * attack;
            }
            else
            {
                _currentRmsLevel = _currentRmsLevel + (rms - _currentRmsLevel) * release;
            }

            _rmsHistory[_rmsHistoryIndex] = _currentRmsLevel;
            _rmsHistoryIndex = (_rmsHistoryIndex + 1) % _rmsHistory.Length;

            RmsLevelChanged?.Invoke(this, _currentRmsLevel);
        }

        public byte[] GetRecordedAudio()
        {
            var buffers = new byte[_audioBuffers.Count][];

            int index = 0;
            while (_audioBuffers.TryDequeue(out byte[]? data))
            {
                buffers[index++] = data;
            }

            int totalSize = 0;
            foreach (var buf in buffers)
            {
                totalSize += buf.Length;
            }

            var allData = new byte[totalSize];
            int offset = 0;
            foreach (var buf in buffers)
            {
                Array.Copy(buf, 0, allData, offset, buf.Length);
                offset += buf.Length;
            }

            return allData;
        }

        public static byte[] EncodeToWav(byte[] pcmData)
        {
            int dataLength = pcmData.Length;
            int headerLength = 44;
            int totalLength = headerLength + dataLength;

            var wav = new byte[totalLength];

            // RIFF 头
            wav[0] = 0x52; // R
            wav[1] = 0x49; // I
            wav[2] = 0x46; // F
            wav[3] = 0x46; // F
            WriteInt32(wav, 4, totalLength - 8);
            wav[8] = 0x57; // W
            wav[9] = 0x41; // A
            wav[10] = 0x56; // V
            wav[11] = 0x45; // E

            // fmt 块
            wav[12] = 0x66; // f
            wav[13] = 0x6D; // m
            wav[14] = 0x74; // t
            wav[15] = 0x20; // (space)
            WriteInt32(wav, 16, 16);
            WriteInt16(wav, 20, 1);
            WriteInt16(wav, 22, Channels);
            WriteInt32(wav, 24, SampleRate);
            WriteInt32(wav, 28, SampleRate * Channels * BitsPerSample / 8);
            WriteInt16(wav, 32, (ushort)(Channels * BitsPerSample / 8));
            WriteInt16(wav, 34, BitsPerSample);

            // data 块
            wav[36] = 0x64; // d
            wav[37] = 0x61; // a
            wav[38] = 0x74; // t
            wav[39] = 0x61; // a
            WriteInt32(wav, 40, dataLength);

            Array.Copy(pcmData, 0, wav, headerLength, dataLength);

            return wav;
        }

        private static void WriteInt16(byte[] data, int offset, ushort value)
        {
            data[offset] = (byte)(value & 0xFF);
            data[offset + 1] = (byte)((value >> 8) & 0xFF);
        }

        private static void WriteInt32(byte[] data, int offset, int value)
        {
            data[offset] = (byte)(value & 0xFF);
            data[offset + 1] = (byte)((value >> 8) & 0xFF);
            data[offset + 2] = (byte)((value >> 16) & 0xFF);
            data[offset + 3] = (byte)((value >> 24) & 0xFF);
        }

        public void Dispose()
        {
            StopCapture();

            _audioClient = null;
            _captureClient = null;
            _device = null;
            _enumerator = null;
        }
    }
}

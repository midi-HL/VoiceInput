using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace VoiceInput
{
    public class AudioCapture : IDisposable
    {
        #region WASAPI COM 接口

        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            [PreserveSig]
            int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppDevice);
        }

        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            [PreserveSig]
            int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
        }

        [ComImport, Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioClient
        {
            [PreserveSig]
            int Initialize(int streamFlags, int bufferDuration, int periodicity, ref WAVEFORMATEX pFormat, Guid audioSessionGuid);

            [PreserveSig]
            int GetBufferSize(out uint pNumBufferFrames);

            [PreserveSig]
            int GetStreamLatency(out long phnsLatency);

            [PreserveSig]
            int GetCurrentPadding(out uint pNumPaddingFrames);

            [PreserveSig]
            int IsFormatSupported(int shareMode, ref WAVEFORMATEX pFormat, out WAVEFORMATEX ppClosestMatch);

            [PreserveSig]
            int GetMixFormat(out WAVEFORMATEX ppDeviceFormat);

            [PreserveSig]
            int GetDevicePeriod(out long phnsDefaultDevicePeriod, out long phnsMinimumDevicePeriod);

            [PreserveSig]
            int Start();

            [PreserveSig]
            int Stop();

            [PreserveSig]
            int Reset();

            [PreserveSig]
            int SetEventHandle(IntPtr eventHandle);

            [PreserveSig]
            int GetService(ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppv);
        }

        [ComImport, Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioCaptureClient
        {
            [PreserveSig]
            int GetBuffer(out IntPtr ppData, out uint pNumFramesToRead, out uint pdwFlags, out long pu64DevicePosition, out long pu64QPCPosition);

            [PreserveSig]
            int ReleaseBuffer(uint numFramesRead);

            [PreserveSig]
            int GetNextPacketSize(out uint pNumFramesInNextPacket);
        }

        [StructLayout(LayoutKind.Sequential)]
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

        private const int CLSCTX_ALL = 23;
        private const int eRender = 0;
        private const int eConsole = 0;
        private const int AUDCLNT_SHAREMODE_SHARED = 0;
        private const int AUDCLNT_STREAMFLAGS_LOOPBACK = 0x00020000;
        private const int WAVE_FORMAT_PCM = 1;

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
        private readonly object _bufferLock = new();

        // RMS 电平计算
        private double _currentRmsLevel;
        private readonly double[] _rmsHistory = new double[60]; // 约 1 秒的历史（16ms 一帧）
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
                Type? enumeratorType = Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E"));
                if (enumeratorType == null) throw new Exception("无法创建音频设备枚举器类型");

                _enumerator = (IMMDeviceEnumerator)Activator.CreateInstance(enumeratorType)!;

                // 获取默认音频端点（渲染端点用于环回捕获）
                int hr = _enumerator.GetDefaultAudioEndpoint(eRender, eConsole, out _device);
                if (hr != 0) throw new COMException("获取默认音频端点失败", hr);

                // 激活音频客户端
                Guid iidAudioClient = new("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2");
                hr = _device.Activate(ref iidAudioClient, CLSCTX_ALL, IntPtr.Zero, out object objAudioClient);
                if (hr != 0) throw new COMException("激活音频客户端失败", hr);

                _audioClient = (IAudioClient)objAudioClient;

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

                // 初始化音频客户端（环回模式）
                hr = _audioClient.Initialize(
                    AUDCLNT_SHAREMODE_SHARED,
                    AUDCLNT_STREAMFLAGS_LOOPBACK,
                    0, // 默认缓冲区时长
                    ref format,
                    Guid.Empty);

                if (hr != 0) throw new COMException("初始化音频客户端失败", hr);

                // 获取捕获客户端
                Guid iidCaptureClient = new("C8ADBD64-E71E-48a0-A4DE-185C395CD317");
                hr = _audioClient.GetService(ref iidCaptureClient, out object objCaptureClient);
                if (hr != 0) throw new COMException("获取捕获客户端失败", hr);

                _captureClient = (IAudioCaptureClient)objCaptureClient;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"初始化音频客户端失败: {ex.Message}");
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
                    int hr = _captureClient.GetNextPacketSize(out uint packetLength);
                    if (hr != 0 || packetLength == 0)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    // 获取缓冲区
                    hr = _captureClient.GetBuffer(out IntPtr dataPtr, out uint framesAvailable, out uint flags, out _, out _);
                    if (hr != 0)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

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
                    System.Diagnostics.Debug.WriteLine($"音频捕获错误: {ex.Message}");
                    Thread.Sleep(10);
                }
            }
        }

        private double CalculateRmsLevel(byte[] buffer, int dataSize)
        {
            if (dataSize == 0) return 0;

            long sumSquares = 0;
            int sampleCount = dataSize / 2; // 16bit = 2 bytes per sample

            for (int i = 0; i < dataSize - 1; i += 2)
            {
                short sample = (short)(buffer[i] | (buffer[i + 1] << 8));
                sumSquares += (long)sample * sample;
            }

            double rms = Math.Sqrt((double)sumSquares / sampleCount);
            return rms / 32768.0; // 归一化到 0-1
        }

        private void UpdateRmsLevel(double rms)
        {
            // 平滑处理：attack 系数 40%，release 系数 15%
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

            // 更新历史记录
            _rmsHistory[_rmsHistoryIndex] = _currentRmsLevel;
            _rmsHistoryIndex = (_rmsHistoryIndex + 1) % _rmsHistory.Length;

            // 触发电平变化事件
            RmsLevelChanged?.Invoke(this, _currentRmsLevel);
        }

        /// <summary>
        /// 获取所有录制的音频数据（PCM 格式）
        /// </summary>
        public byte[] GetRecordedAudio()
        {
            var allData = new byte[0];
            var buffers = new byte[_audioBuffers.Count][];

            int index = 0;
            while (_audioBuffers.TryDequeue(out byte[]? data))
            {
                buffers[index++] = data;
            }

            // 计算总大小
            int totalSize = 0;
            foreach (var buf in buffers)
            {
                totalSize += buf.Length;
            }

            // 合并缓冲区
            allData = new byte[totalSize];
            int offset = 0;
            foreach (var buf in buffers)
            {
                Array.Copy(buf, 0, allData, offset, buf.Length);
                offset += buf.Length;
            }

            return allData;
        }

        /// <summary>
        /// 将 PCM 数据编码为 WAV 格式
        /// </summary>
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
            WriteInt32(wav, 16, 16); // 块大小
            WriteInt16(wav, 20, 1); // PCM 格式
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

            // 复制音频数据
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

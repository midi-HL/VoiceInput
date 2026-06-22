using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;

namespace VoiceInput
{
    public class AudioCapture : IDisposable
    {
        private const int SampleRate = 16000;
        private const int Channels = 1;
        private const int BitsPerSample = 16;

        private WaveInEvent? _waveIn;
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
            try
            {
                Logger.Info("AudioCapture: 初始化音频捕获...");
                InitializeAudioCapture();
                Logger.Info("AudioCapture: 初始化完成");
            }
            catch (Exception ex)
            {
                Logger.Error("AudioCapture: 初始化失败", ex);
                throw;
            }
        }

        private void InitializeAudioCapture()
        {
            // 检查是否有可用的音频设备
            int deviceCount = WaveInEvent.DeviceCount;
            Logger.Info($"AudioCapture: 找到 {deviceCount} 个音频输入设备");

            if (deviceCount == 0)
            {
                throw new Exception("未找到音频输入设备。请检查：\n1. 麦克风是否已连接\n2. 麦克风是否已启用\n3. 应用是否有麦克风权限");
            }

            // 列出所有设备
            for (int i = 0; i < deviceCount; i++)
            {
                var deviceInfo = WaveInEvent.GetCapabilities(i);
                Logger.Info($"AudioCapture: 设备 {i}: {deviceInfo.ProductName}");
            }

            // 创建 WaveInEvent
            _waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(SampleRate, BitsPerSample, Channels),
                BufferMilliseconds = 20
            };

            // 设置数据接收事件
            _waveIn.DataAvailable += OnDataAvailable;
            _waveIn.RecordingStopped += OnRecordingStopped;
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            if (e.BytesRecorded == 0) return;

            try
            {
                // 复制音频数据
                var audioData = new byte[e.BytesRecorded];
                Array.Copy(e.Buffer, audioData, e.BytesRecorded);

                // 计算 RMS 电平
                double rms = CalculateRmsLevel(audioData, e.BytesRecorded);
                UpdateRmsLevel(rms);

                // 存储音频数据
                _audioBuffers.Enqueue(audioData);

                // 触发事件
                AudioBufferReady?.Invoke(this, audioData);
            }
            catch (Exception ex)
            {
                Logger.Error("AudioCapture: 处理音频数据失败", ex);
            }
        }

        private void OnRecordingStopped(object? sender, StoppedEventArgs e)
        {
            if (e.Exception != null)
            {
                Logger.Error("AudioCapture: 录音停止时发生错误", e.Exception);
            }
        }

        public void StartCapture()
        {
            if (_isCapturing) return;

            try
            {
                // 清空缓冲区
                while (_audioBuffers.TryDequeue(out _)) { }

                // 开始录音
                _waveIn?.StartRecording();
                _isCapturing = true;
                Logger.Info("AudioCapture: 开始录音");
            }
            catch (Exception ex)
            {
                Logger.Error("AudioCapture: 开始录音失败", ex);
                throw;
            }
        }

        public void StopCapture()
        {
            if (!_isCapturing) return;

            try
            {
                _waveIn?.StopRecording();
                _isCapturing = false;
                Logger.Info("AudioCapture: 停止录音");
            }
            catch (Exception ex)
            {
                Logger.Error("AudioCapture: 停止录音失败", ex);
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
            var allData = new byte[totalSize];
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
            using var memoryStream = new MemoryStream();
            using var writer = new BinaryWriter(memoryStream);

            // RIFF 头
            writer.Write("RIFF".ToCharArray());
            writer.Write((int)(36 + pcmData.Length));
            writer.Write("WAVE".ToCharArray());

            // fmt 块
            writer.Write("fmt ".ToCharArray());
            writer.Write((int)16); // 块大小
            writer.Write((short)1); // PCM 格式
            writer.Write((short)Channels);
            writer.Write((int)SampleRate);
            writer.Write((int)(SampleRate * Channels * BitsPerSample / 8));
            writer.Write((short)(Channels * BitsPerSample / 8));
            writer.Write((short)BitsPerSample);

            // data 块
            writer.Write("data".ToCharArray());
            writer.Write((int)pcmData.Length);
            writer.Write(pcmData);

            return memoryStream.ToArray();
        }

        public void Dispose()
        {
            StopCapture();

            if (_waveIn != null)
            {
                _waveIn.DataAvailable -= OnDataAvailable;
                _waveIn.RecordingStopped -= OnRecordingStopped;
                _waveIn.Dispose();
                _waveIn = null;
            }
        }
    }
}

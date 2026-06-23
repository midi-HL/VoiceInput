using System;
using NAudio.Wave;

namespace VoiceInput
{
    public class AudioCapture : IDisposable
    {
        private WaveInEvent? _waveIn;
        private WaveFileWriter? _writer;
        private string? _currentFilePath;
        private bool _isRecording;
        private bool _disposed;
        private readonly object _lock = new object();
        private float _currentRmsLevel;

        public event Action<float>? RmsLevelChanged;
        public event Action<byte[]>? AudioDataAvailable;

        public bool IsRecording => _isRecording;

        public void StartRecording()
        {
            lock (_lock)
            {
                if (_isRecording) return;

                _currentFilePath = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    $"voiceinput_{DateTime.Now:yyyyMMdd_HHmmss}.wav");

                _waveIn = new WaveInEvent
                {
                    WaveFormat = new WaveFormat(16000, 16, 1) // 16kHz, mono, 16bit PCM
                };

                _waveIn.DataAvailable += OnDataAvailable;
                _waveIn.RecordingStopped += OnRecordingStopped;

                _writer = new WaveFileWriter(_currentFilePath, _waveIn.WaveFormat);

                _waveIn.StartRecording();
                _isRecording = true;
            }
        }

        public string? StopRecording()
        {
            lock (_lock)
            {
                if (!_isRecording) return _currentFilePath;

                _isRecording = false;

                _waveIn?.StopRecording();

                _writer?.Flush();
                _writer?.Dispose();
                _writer = null;

                _waveIn?.Dispose();
                _waveIn = null;

                _currentRmsLevel = 0;
                RmsLevelChanged?.Invoke(0);

                return _currentFilePath;
            }
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            if (!_isRecording) return;

            // Calculate RMS level
            float rms = CalculateRms(e.Buffer, e.BytesRecorded);
            _currentRmsLevel = rms;
            RmsLevelChanged?.Invoke(rms);

            // Write to file
            _writer?.Write(e.Buffer, 0, e.BytesRecorded);

            // Notify audio data available
            byte[] data = new byte[e.BytesRecorded];
            Array.Copy(e.Buffer, data, e.BytesRecorded);
            AudioDataAvailable?.Invoke(data);
        }

        private void OnRecordingStopped(object? sender, StoppedEventArgs e)
        {
            if (e.Exception != null)
            {
                System.Diagnostics.Debug.WriteLine($"Recording error: {e.Exception.Message}");
            }
        }

        private static float CalculateRms(byte[] buffer, int bytesRecorded)
        {
            if (bytesRecorded == 0) return 0;

            float sum = 0;
            int sampleCount = bytesRecorded / 2; // 16-bit samples

            for (int i = 0; i < bytesRecorded - 1; i += 2)
            {
                short sample = (short)(buffer[i] | (buffer[i + 1] << 8));
                float normalized = sample / 32768f;
                sum += normalized * normalized;
            }

            return (float)Math.Sqrt(sum / sampleCount);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                StopRecording();
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }

        ~AudioCapture()
        {
            Dispose();
        }
    }
}

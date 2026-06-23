using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace VoiceInput.Pages
{
    public sealed partial class LyricsPage : Page
    {
        private string? _selectedFilePath;
        private CancellationTokenSource? _cts;
        private readonly string[] _supportedExtensions = { ".mp3", ".wav", ".flac", ".ogg", ".m4a", ".aac" };

        public LyricsPage()
        {
            this.InitializeComponent();
        }

        private async void UploadArea_Tapped(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            picker.SuggestedStartLocation = PickerLocationId.MusicLibrary;
            foreach (var ext in _supportedExtensions)
            {
                picker.FileTypeFilter.Add(ext);
            }

            // WinUI 3 requires HWND for FileOpenPicker
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                await LoadFileAsync(file.Path);
            }
        }

        private async void UploadArea_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = DataPackageOperations.Copy;
            e.DragUIOverride.Caption = "拖放以上传音频文件";
            e.DragUIOverride.IsCaptionVisible = true;
            e.DragUIOverride.IsContentVisible = true;
            await Task.CompletedTask;
        }

        private async void UploadArea_Drop(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                if (items.Count > 0)
                {
                    var file = items[0] as StorageFile;
                    if (file != null)
                    {
                        string ext = Path.GetExtension(file.Path).ToLowerInvariant();
                        if (Array.Exists(_supportedExtensions, e => e == ext))
                        {
                            await LoadFileAsync(file.Path);
                        }
                    }
                }
            }
        }

        private Task LoadFileAsync(string filePath)
        {
            _selectedFilePath = filePath;

            UploadPanel.Visibility = Visibility.Collapsed;
            FileInfoPanel.Visibility = Visibility.Visible;

            FileNameText.Text = Path.GetFileName(filePath);

            var fileInfo = new FileInfo(filePath);
            string size = FormatFileSize(fileInfo.Length);
            FileDetailsText.Text = $"大小: {size}";

            // Reset result state
            PlaceholderText.Visibility = Visibility.Visible;
            LoadingPanel.Visibility = Visibility.Collapsed;
            ResultGrid.Visibility = Visibility.Collapsed;

            return Task.CompletedTask;
        }

        private void Reselect_Click(object sender, RoutedEventArgs e)
        {
            _selectedFilePath = null;
            UploadPanel.Visibility = Visibility.Visible;
            FileInfoPanel.Visibility = Visibility.Collapsed;

            PlaceholderText.Visibility = Visibility.Visible;
            LoadingPanel.Visibility = Visibility.Collapsed;
            ResultGrid.Visibility = Visibility.Collapsed;
        }

        private async void StartRecognize_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedFilePath)) return;

            StartRecognizeButton.IsEnabled = false;
            PlaceholderText.Visibility = Visibility.Collapsed;
            LoadingPanel.Visibility = Visibility.Visible;
            ResultGrid.Visibility = Visibility.Collapsed;

            _cts = new CancellationTokenSource();

            try
            {
                var recognizer = new LyricsRecognizer();
                recognizer.ProgressUpdated += (status) =>
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        LoadingText.Text = status;
                    });
                };

                string result = await recognizer.RecognizeAsync(_selectedFilePath, _cts.Token);

                DispatcherQueue.TryEnqueue(() =>
                {
                    LoadingPanel.Visibility = Visibility.Collapsed;
                    ResultGrid.Visibility = Visibility.Visible;
                    LyricsText.Text = result;
                });

                recognizer.Dispose();
            }
            catch (OperationCanceledException)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    LoadingPanel.Visibility = Visibility.Collapsed;
                    PlaceholderText.Visibility = Visibility.Visible;
                    PlaceholderText.Text = "识别已取消";
                });
            }
            catch (Exception ex)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    LoadingPanel.Visibility = Visibility.Collapsed;
                    PlaceholderText.Visibility = Visibility.Visible;
                    PlaceholderText.Text = $"识别失败: {ex.Message}";
                });
            }
            finally
            {
                StartRecognizeButton.IsEnabled = true;
            }
        }

        private async void CopyAll_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(LyricsText.Text))
            {
                var package = new DataPackage();
                package.SetText(LyricsText.Text);
                Clipboard.SetContent(package);

                var dialog = new ContentDialog
                {
                    Title = "复制成功",
                    Content = "歌词已复制到剪贴板",
                    CloseButtonText = "确定",
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
            }
        }

        private async void ExportLrc_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(LyricsText.Text)) return;

            var picker = new FileSavePicker();
            picker.SuggestedStartLocation = PickerLocationId.MusicLibrary;
            picker.FileTypeChoices.Add("LRC 文件", new[] { ".lrc" });
            picker.SuggestedFileName = "lyrics";

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSaveFileAsync();
            if (file != null)
            {
                await File.WriteAllTextAsync(file.Path, LyricsText.Text);

                var dialog = new ContentDialog
                {
                    Title = "导出成功",
                    Content = $"歌词已导出到:\n{file.Path}",
                    CloseButtonText = "确定",
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
            }
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            LyricsText.Text = "";
            ResultGrid.Visibility = Visibility.Collapsed;
            PlaceholderText.Visibility = Visibility.Visible;
            PlaceholderText.Text = "识别出的歌词将显示在这里";
        }

        private static string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
            return $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
        }
    }
}

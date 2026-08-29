using DJCMS10.Models;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DJCMS.Models
{
    public class PlaylistTrack : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public Guid ID { get; set; } = Guid.NewGuid();

        private string _filePath = "";
        private string? _cachedDuration;
        private ImageSource? _cachedThumbnail;

        public string FilePath
        {
            get => _filePath;
            set
            {
                if (_filePath == value)
                    return;

                _filePath = value;
                _cachedDuration = null; // Invalidate cache

                try
                {
                    using var file = TagLib.File.Create(FilePath);

                    Title = file.Tag.Title;
                    Artist = string.Join(',', file.Tag.Performers);

                    TimeSpan duration = file.Properties.Duration;
                    TotalSeconds = (int)duration.TotalSeconds;
                }
                catch
                {
                    // TagLib failed; try a simpler reader. If that also fails, show an error dialog
                    try
                    {
                        Title = FileName;
                        using var reader = new NAudio.Wave.AudioFileReader(FilePath);
                        var totalSeconds = (int)reader.TotalTime.TotalSeconds;
                        TotalSeconds = totalSeconds;
                        reader.Close();
                        reader.Dispose();
                    }
                    catch (Exception ex)
                    {
                        // Show a simple error dialog similar to ConfirmDialog
                        try
                        {
                            var message = $"Cannot open this file in the application:\n{Path.GetFileName(FilePath)}\n\n{ex.Message}";
                            Application.Current?.Dispatcher?.Invoke(() =>
                            {
                                var dlg = new DJCMS.Views.ErrorDialog(message);
                                dlg.Owner = Application.Current?.MainWindow;
                                dlg.ShowDialog();
                            });
                        }
                        catch
                        {
                            // If showing the dialog fails for any reason, swallow to avoid crash
                        }

                        throw;
                    }
                }

                OnPropertyChanged();
                OnPropertyChanged(nameof(FileName));
                OnPropertyChanged(nameof(Duration));
                OnPropertyChanged(nameof(Thumbnail));
                OnPropertyChanged(nameof(Title));
                OnPropertyChanged(nameof(Artist));
                OnPropertyChanged(nameof(TotalSeconds));
            }
        }

        private int _gapSeconds;
        public int GapSeconds
        {
            get => _gapSeconds;
            set
            {
                if (_gapSeconds == value)
                    return;

                _gapSeconds = value;
                OnPropertyChanged();
            }
        }

        private bool _fadeInOnCross;
        public bool FadeInOnCross
        {
            get => _fadeInOnCross;
            set
            {
                if (_fadeInOnCross == value)
                    return;

                _fadeInOnCross = value;
                OnPropertyChanged();
            }
        }

        [JsonIgnore]
        public int TotalSeconds { get; set; }

        [JsonIgnore]
        public string Duration
        {
            get
            {
                if (_cachedDuration != null)
                    return _cachedDuration;

                try
                {
                    if (string.IsNullOrEmpty(FilePath) || !System.IO.File.Exists(FilePath))
                    {
                        _cachedDuration = "00:00";
                        return _cachedDuration;
                    }
                                     

                    var totalSeconds = (int)TotalSeconds;
                    var minutes = totalSeconds / 60;
                    var seconds = totalSeconds % 60;
                    _cachedDuration = $"{minutes}:{seconds:D2}";
                    return _cachedDuration;
                }
                catch (Exception e)
                {
                    _cachedDuration = "00:00";
                    return _cachedDuration;
                }
            }
        }

        [JsonIgnore]
        public string Title { get; set; }

        [JsonIgnore]
        public string Artist { get; set; }

        [JsonIgnore]
        public string FileName => string.IsNullOrWhiteSpace(Title)? Path.GetFileNameWithoutExtension(FilePath) : Title;

        [JsonIgnore]
        public ImageSource Thumbnail
        {
            get
            {
                if (_cachedThumbnail != null)
                    return _cachedThumbnail;

                var localImage = Path.Combine(Path.GetDirectoryName(FilePath) ?? "", Path.GetFileNameWithoutExtension(FilePath) + ".png");
                if (File.Exists(localImage))
                {
                    _cachedThumbnail = new BitmapImage(new Uri(localImage, UriKind.RelativeOrAbsolute));
                    return _cachedThumbnail;
                }
                var appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DJCMS", "Thumbnails");
                var appDataImage = Path.Combine(appDataFolder, Path.GetFileNameWithoutExtension(FilePath) + ".png");
                if (File.Exists(appDataImage))
                {
                    _cachedThumbnail = new BitmapImage(new Uri(appDataImage, UriKind.RelativeOrAbsolute));
                    return _cachedThumbnail;
                }

                _cachedThumbnail = TrackThumbnailGenerator.Generate(ID.ToString());
                return _cachedThumbnail;
            }
        }

        public void ReGen()
        {
            ID = Guid.NewGuid();
            _cachedThumbnail = null; // Invalidate thumbnail cache
            OnPropertyChanged(nameof(ID));
            OnPropertyChanged(nameof(Thumbnail));
        }
    }
}

using DJCMS10.Models;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
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
                OnPropertyChanged();
                OnPropertyChanged(nameof(FileName));
                OnPropertyChanged(nameof(Duration));
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

                    using var reader = new NAudio.Wave.AudioFileReader(FilePath);
                    var totalSeconds = (int)reader.TotalTime.TotalSeconds;
                    var minutes = totalSeconds / 60;
                    var seconds = totalSeconds % 60;
                    _cachedDuration = $"{minutes}:{seconds:D2}";
                    return _cachedDuration;
                }
                catch
                {
                    _cachedDuration = "00:00";
                    return _cachedDuration;
                }
            }
        }

        [JsonIgnore]
        public string FileName => System.IO.Path.GetFileNameWithoutExtension(FilePath);

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

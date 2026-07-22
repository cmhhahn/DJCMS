using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DJCMS10.Models;

public class PlaylistFile : INotifyPropertyChanged
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

            _cachedThumbnail = TrackThumbnailGenerator.Generate(FileName);
            return _cachedThumbnail;
        }
    }
}

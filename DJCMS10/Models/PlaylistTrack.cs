using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

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
        public string FilePath
        {
            get => _filePath;
            set
            {
                if (_filePath == value)
                    return;

                _filePath = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FileName));
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

        public string FileName => System.IO.Path.GetFileNameWithoutExtension(FilePath);
    }
}

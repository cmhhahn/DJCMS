using Caliburn.Micro;
using DJCMS.Models;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;

namespace DJCMS.ViewModels
{
    public class MainWindowViewModel : Screen
    {
        private readonly DispatcherTimer _timer;
        private readonly MixingSampleProvider _mixer;
        private readonly WaveOutEvent _output;
        private readonly WaveFormat _mixerFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);

        private PlayingTrack? _currentTrack;
        private PlayingTrack? _bufferTrack;
        private double _progress;
        private bool _dragging;
        private Guid? _selectionID;
        private double _volume = 0.5;

        public MainWindowViewModel()
        {
            Tracks = new ObservableCollection<PlaylistTrack>();

            _output = new WaveOutEvent();
            _mixer = new MixingSampleProvider(_mixerFormat)
            {
                ReadFully = false
            };
            try
            {
                _output.Volume = (float)_volume;

                _output.Init(_mixer);
                //_output.Play();

                _output.PlaybackStopped += (s, e) =>
                {
                    IsPlaying = false;
                };
            }
            catch
            {
                // Audio initialization failed (device or platform issue). Swallow so UI can load.
                // Leave _mixer initialized but don't rely on _output being usable.
            }

            _timer = new DispatcherTimer(
                TimeSpan.FromMilliseconds(60),
                DispatcherPriority.Normal,
                OnTimerTick,
                Application.Current.Dispatcher);
            _timer.Start();

            AutoLoad();
        }

        private ObservableCollection<PlaylistTrack> _libraryFolder;
        public ObservableCollection<PlaylistTrack> LibraryFolder
        {
            get
            {
                return _libraryFolder;
            }
            set
            {
                _libraryFolder = value;
                NotifyOfPropertyChange();
            }
        }

        private ObservableCollection<PlaylistTrack> _tracks;
        public ObservableCollection<PlaylistTrack> Tracks
        {
            get
            {
                return _tracks;
            }
            set
            {
                _tracks = value;
                NotifyOfPropertyChange();
            }
        }

        private double _trackProgress;
        public double TrackProgress
        {
            get => _trackProgress;
            set
            {
                if (Math.Abs(_trackProgress - value) < 0.0001)
                    return;
                _trackProgress = value;
                NotifyOfPropertyChange();
            }
        }

        public double Volume
        {
            get => _volume;
            set
            {
                if (Math.Abs(_volume - value) < 0.0001)
                    return;
                _volume = Math.Max(0, Math.Min(1, value));
                try
                {
                    _output.Volume = (float)_volume;
                }
                catch
                {
                    // ignore if output not initialized yet
                }
                NotifyOfPropertyChange();
            }
        }

        private PlaylistTrack? _selectedTrack;
        public PlaylistTrack? SelectedTrack
        {
            get => _selectedTrack;
            set
            {
                if (_selectedTrack == value)
                    return;
                _selectedTrack = value;
                _selectionID = value?.ID;
                NotifyOfPropertyChange();
            }
        }

        public void Drop(DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
                return;

            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            LoadFiles(files);
        }

        private bool _isPlaying;
        public bool IsPlaying
        {
            get => _isPlaying;
            set
            {
                if (_isPlaying == value)
                    return;
                _isPlaying = value;
                NotifyOfPropertyChange();
            }
        }

        public void Pause()
        {
            _output.Pause();
        }

        public void TogglePlay()
        {
            if (IsPlaying)
            {
                Stop();
            }
            else
            {
                Play();                
            }
        }

        public void Play()
        {
            if(_currentTrack == null && _selectionID == null)
            {
                var firstTrack = Tracks.FirstOrDefault();
                if (firstTrack != null)
                {
                    _selectionID = firstTrack.ID;
                    SelectedTrack = firstTrack;
                }
            }

            if (_selectionID == null)
                return;

            if (_selectionID == _currentTrack?.TrackID)
            {
                _output.Play();
                IsPlaying = true;
            }
            else
            {
                _mixer.RemoveAllMixerInputs();

                var track = GetListTrack(_selectionID.Value);
                if (track != null)
                {
                    _currentTrack = new PlayingTrack(track, 0);
                    _mixer.AddMixerInput(_currentTrack.Provider);
                    _output.Play();
                    IsPlaying = true;
                }
            }
        }

        public void Stop()
        {
            _output.Stop();
        }

        public void AddTrack(Guid trackId)
        {
            
        }

        public void Minus(Guid trackId)
        {
            var track = GetListTrack(trackId);
            if (track != null)
            {
                track.GapSeconds--;
            }
        }

        public void Plus(Guid trackId)
        {
            var track = GetListTrack(trackId);
            if (track != null)
            {
                track.GapSeconds++;
            }
        }

        public void TrackProgressDragStarted()
        {
            _dragging = true;
        }

        public void TrackProgressDragCompleted()
        {
            try
            {
                if (_currentTrack != null)
                {
                    var newProgress = TrackProgress;
                    _currentTrack.Seek(TimeSpan.FromMilliseconds(newProgress * _currentTrack.TotalTime.TotalMilliseconds));
                }
                _dragging = false;
            }
            catch
            {
                _dragging = false;
            }
        }

        private void OnTimerTick(object? sender, EventArgs e)
        {
            if (_currentTrack != null &&
                _currentTrack.CurrentTime.TotalMilliseconds > 0 &&
                _currentTrack.TotalTime.TotalMilliseconds > 0)
            {
                double progress = _currentTrack.CurrentTime.TotalMilliseconds /
                                  _currentTrack.TotalTime.TotalMilliseconds;

                if (!_dragging)
                {
                    TrackProgress = progress;
                }

                MonitorPlayingEvents(progress);
            }
            else
            {
                TrackProgress = 0;
            }
        }

        private void MonitorPlayingEvents(double progress)
        {
            if (_progress == progress)
                return;

            if (progress > 0.99 && !_mixer.MixerInputs.Any())
            {
                if (_currentTrack?.Track.GapSeconds > -1)
                {
                    var nextTrack = GetNextTrack();
                    if (nextTrack != null)
                    {
                        PlayTrack(nextTrack, _currentTrack.Track.GapSeconds);
                        _currentTrack = _bufferTrack;
                        _output.Play();
                        IsPlaying = true;
                        SelectedTrack = nextTrack;
                    }
                    else
                    {
                        _output.Stop();
                        IsPlaying = false;
                    }
                }
            }

            _progress = progress;
        }

        private PlaylistTrack? GetNextTrack()
        {
            if (_currentTrack == null)
                return Tracks.FirstOrDefault();

            var result = Tracks.IndexOf(_currentTrack.Track);
            if (result > -1 && Tracks.Count > result + 1)
            {
                return Tracks[result + 1];
            }

            return null;
        }

        private void PlayTrack(PlaylistTrack track, int offset)
        {
            _bufferTrack = new PlayingTrack(track, offset);
            _mixer.AddMixerInput(_bufferTrack.Provider);
        }

        private void AutoLoad()
        {
            var libraryFolderPath = @"D:\Music\DJing";
            var folderPath = @"D:\Music\DJing\test";

            var supportedExtensions = new[] { ".mp3", ".wav", ".m4a", ".flac", ".aac", ".wma", ".ogg" };

            var fileArray = Directory.GetFiles(folderPath)
                .Where(file => supportedExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                .OrderBy(file => file)
                .ToArray();

            LoadFiles(fileArray);

            var fileArray2 = Directory.GetFiles(libraryFolderPath)
                .Where(file => supportedExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                .OrderBy(file => file)
                .ToArray();

            LoadFiles(fileArray);
            LoadLibrary(fileArray2);
        }

        public void LoadFiles(string[] files)
        {
            foreach (var file in files)
            {
                Tracks.Add(new PlaylistTrack { FilePath = file, GapSeconds = 0 });
            }
        }

        public void LoadLibrary(string[] files)
        {
            LibraryFolder = new ObservableCollection<PlaylistTrack>();
            foreach (var file in files)
            {
                LibraryFolder.Add(new PlaylistTrack { FilePath = file, GapSeconds = 0 });
            }
        }

        public void LoadFilesAtIndex(string[] files, int index)
        {
            if (index < 0 || index > Tracks.Count)
                index = Tracks.Count;

            foreach (var file in files)
            {
                Tracks.Insert(index, new PlaylistTrack { FilePath = file, GapSeconds = 0 });
                index++;
            }
        }

        private PlaylistTrack? GetListTrack(Guid id)
        {
            return Tracks.FirstOrDefault(t => t.ID == id);
        }

        // TODO: Implement saving/loading playlists. Wired to the view via Caliburn Micro actions.
        public async void SavePlaylist()
        {
            // Intentionally left blank for implementation in ViewModel
            await SavePlaylistAsync(Tracks);
        }

        public async void LoadPlaylist()
        {
            Stop();            
            _currentTrack = null;
            _bufferTrack = null;
            _selectedTrack = null;
            _selectionID = null;
            _trackProgress = 0;
            Tracks = await LoadPlaylistAsync() ?? new ObservableCollection<PlaylistTrack>();
        }

        public static async Task SavePlaylistAsync(
    ObservableCollection<PlaylistTrack> tracks)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "DJ Playlist (*.json)|*.json|All Files (*.*)|*.*",
                DefaultExt = ".json",
                AddExtension = true,
                FileName = "playlist.json"
            };

            if (dialog.ShowDialog() != true)
                return;

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            string json = JsonSerializer.Serialize(tracks, options);

            await File.WriteAllTextAsync(dialog.FileName, json);
        }

        public static async Task<ObservableCollection<PlaylistTrack>?>
    LoadPlaylistAsync()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "DJ Playlist (*.json)|*.json|All Files (*.*)|*.*"
            };

            if (dialog.ShowDialog() != true)
                return null;

            string json = await File.ReadAllTextAsync(dialog.FileName);

            var tracks =
                JsonSerializer.Deserialize<
                    ObservableCollection<PlaylistTrack>>(json);

            return tracks;
        }

        protected override Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
        {
            if (close)
            {
                _timer.Stop();
                _output.Stop();
                _output.Dispose();
                _mixer.RemoveAllMixerInputs();
                _currentTrack?.Reader.Dispose();
                _bufferTrack?.Reader.Dispose();
            }
            return base.OnDeactivateAsync(close, cancellationToken);
        }
    }
}

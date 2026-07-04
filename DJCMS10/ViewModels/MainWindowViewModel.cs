using Caliburn.Micro;
using DJCMS.Models;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

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

        public MainWindowViewModel()
        {
            Tracks = new ObservableCollection<PlaylistTrack>();

            _output = new WaveOutEvent();
            _mixer = new MixingSampleProvider(_mixerFormat)
            {
                ReadFully = false
            };

            _output.Init(_mixer);
            _output.Play();

            _timer = new DispatcherTimer(
                TimeSpan.FromMilliseconds(60),
                DispatcherPriority.Normal,
                OnTimerTick,
                Application.Current.Dispatcher);
            _timer.Start();

            AutoLoad();
        }

        public ObservableCollection<PlaylistTrack> Tracks { get; }

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

        public void Play()
        {
            if (_selectionID == null)
                return;

            if (_selectionID == _currentTrack?.TrackID)
            {
                _output.Play();
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
                }
            }
        }

        public void Stop()
        {
            _output.Stop();
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
                        SelectedTrack = nextTrack;
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
            var folderPath = @"D:\Music\DJing\test";
            if (!Directory.Exists(folderPath))
                return;

            var supportedExtensions = new[] { ".mp3", ".wav", ".m4a", ".flac", ".aac", ".wma", ".ogg" };
            var fileArray = Directory.GetFiles(folderPath)
                .Where(file => supportedExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                .OrderBy(file => file)
                .ToArray();

            LoadFiles(fileArray);
        }

        public void LoadFiles(string[] files)
        {
            foreach (var file in files)
            {
                Tracks.Add(new PlaylistTrack { FilePath = file, GapSeconds = 0 });
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

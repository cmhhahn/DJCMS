using Caliburn.Micro;
using DJCMS.Models;
using DJCMS10.Models;
using Microsoft.Win32;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using Serilog;

namespace DJCMS.ViewModels
{
    public class MainWindowViewModel : Screen
    {
        private readonly IEventAggregator _eventAggregator;
        private readonly ILogger _logger;
        private readonly DispatcherTimer _timer;
        private readonly MixingSampleProvider _mixerX;
        private readonly EqualizerSampleProvider _equalizer;
        private readonly WaveOutEvent _output;
        private readonly WaveFormat _mixerFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);

        private PlayingTrack? _currentTrack;
        private PlayingTrack? _bufferTrack;
        private double _progress;
        private bool _dragging;
        private Guid? _selectionID;
        private double _volume = 0.5;

        // Equalizer band gains (in dB)
        private float _band0 = 0f;
        private float _band1 = 0f;
        private float _band2 = 0f;
        private float _band3 = 0f;
        private float _band4 = 0f;
        private float _band5 = 0f;
        private float _band6 = 0f;
        private float _band7 = 0f;
        private float _band8 = 0f;
        private float _band9 = 0f;

        private string _currentTime = "0:00";
        public string CurrentTime
        {
            get => _currentTime;
            set
            {
                if (_currentTime == value)
                    return;
                _currentTime = value;
                NotifyOfPropertyChange();
            }
        }

        private string _totalTime = "0:00";
        public string TotalTime
        {
            get => _totalTime;
            set
            {
                if (_totalTime == value)
                    return;
                _totalTime = value;
                NotifyOfPropertyChange();
            }
        }

        public MainWindowViewModel(ILogger logger, IEventAggregator eventAggregator)
        {
            _logger = logger?.ForContext<MainWindowViewModel>() ?? Log.Logger.ForContext<MainWindowViewModel>();
            _logger.Information("================================================================================================");
            _logger.Information("MainWindowViewModel starting up");
            _logger.Information("------------------------------------------------------------------------------------------------");
            _eventAggregator = eventAggregator;

            Tracks = new ObservableCollection<PlaylistTrack>();

            // Ensure library collection is initialized to avoid null refs
            LibraryFolder = new ObservableCollection<PlaylistTrack>();

            _output = new WaveOutEvent();
            _mixerX = new MixingSampleProvider(_mixerFormat)
            {
                ReadFully = false
            };

            // Create equalizer and wire it to the mixer
            _equalizer = new EqualizerSampleProvider(_mixerX);

            try
            {
                _output.Volume = (float)_volume;

                _output.Init(_equalizer);
                //_output.Play();

                // Ensure any UI property changes happen on the UI thread
                _output.PlaybackStopped += (s, e) =>
                {
                    try
                    {
                        Application.Current?.Dispatcher?.BeginInvoke(new System.Action(() => IsPlaying = false));
                    }
                    catch
                    {
                        // ignore dispatcher problems
                    }
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
            _logger.Information("MainWindowViewModel initialized");
        }

        private ObservableCollection<PlaylistFile> _playlistLibrary;
        public ObservableCollection<PlaylistFile> PlaylistLibrary
        {
            get => _playlistLibrary;
            set
            {
                _playlistLibrary = value;
                NotifyOfPropertyChange();
            }
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
                NotifyOfChangeAsyncDelay(nameof(PlaylistTime));
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

        // Equalizer Band Properties (20Hz - 20kHz)
        public float Band0
        {
            get => _band0;
            set
            {
                if (Math.Abs(_band0 - value) < 0.01f)
                    return;
                _band0 = value;
                _equalizer?.SetGain(0, value);
                NotifyOfPropertyChange();
            }
        }

        public float Band1
        {
            get => _band1;
            set
            {
                if (Math.Abs(_band1 - value) < 0.01f)
                    return;
                _band1 = value;
                _equalizer?.SetGain(1, value);
                NotifyOfPropertyChange();
            }
        }

        public float Band2
        {
            get => _band2;
            set
            {
                if (Math.Abs(_band2 - value) < 0.01f)
                    return;
                _band2 = value;
                _equalizer?.SetGain(2, value);
                NotifyOfPropertyChange();
            }
        }

        public float Band3
        {
            get => _band3;
            set
            {
                if (Math.Abs(_band3 - value) < 0.01f)
                    return;
                _band3 = value;
                _equalizer?.SetGain(3, value);
                NotifyOfPropertyChange();
            }
        }

        public float Band4
        {
            get => _band4;
            set
            {
                if (Math.Abs(_band4 - value) < 0.01f)
                    return;
                _band4 = value;
                _equalizer?.SetGain(4, value);
                NotifyOfPropertyChange();
            }
        }

        public float Band5
        {
            get => _band5;
            set
            {
                if (Math.Abs(_band5 - value) < 0.01f)
                    return;
                _band5 = value;
                _equalizer?.SetGain(5, value);
                NotifyOfPropertyChange();
            }
        }

        public float Band6
        {
            get => _band6;
            set
            {
                if (Math.Abs(_band6 - value) < 0.01f)
                    return;
                _band6 = value;
                _equalizer?.SetGain(6, value);
                NotifyOfPropertyChange();
            }
        }

        public float Band7
        {
            get => _band7;
            set
            {
                if (Math.Abs(_band7 - value) < 0.01f)
                    return;
                _band7 = value;
                _equalizer?.SetGain(7, value);
                NotifyOfPropertyChange();
            }
        }

        public float Band8
        {
            get => _band8;
            set
            {
                if (Math.Abs(_band8 - value) < 0.01f)
                    return;
                _band8 = value;
                _equalizer?.SetGain(8, value);
                NotifyOfPropertyChange();
            }
        }

        public float Band9
        {
            get => _band9;
            set
            {
                if (Math.Abs(_band9 - value) < 0.01f)
                    return;
                _band9 = value;
                _equalizer?.SetGain(9, value);
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

        private Guid? _currentPlayingTrackId;
        public Guid? CurrentPlayingTrackId
        {
            get => _currentPlayingTrackId;
            set
            {
                if (_currentPlayingTrackId == value)
                    return;
                _currentPlayingTrackId = value;
                NotifyOfPropertyChange();
            }
        }

        public void Pause()
        {
            try
            {
                _output?.Pause();
            }
            catch
            {
                // ignore audio errors
            }
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
                try
                {
                    _output?.Play();
                    IsPlaying = true;
                    CurrentPlayingTrackId = _selectionID;
                }
                catch
                {
                    IsPlaying = false;
                }
            }
            else
            {
                PlayTrack(_selectionID.Value);
            }
        }

        public void Stop()
        {
            try
            {
                _output?.Stop();
                IsPlaying = false;
                CurrentPlayingTrackId = null;
            }
            catch
            {
                // ignore
            }
        }


        private Tuple<Guid, DateTime> clickyPTime = new Tuple<Guid, DateTime>(Guid.Empty, DateTime.MinValue);

        public void ClickyPlaylist(Guid listId)
        {
            if (clickyPTime.Item1 == listId && (DateTime.Now - clickyPTime.Item2).TotalMilliseconds < 200)
            {
                LoadAPlaylistFile(GetPlaylistFromId(listId)?.FilePath??"");
            }

            clickyPTime = new Tuple<Guid, DateTime>(listId, DateTime.Now);
        }


        private Tuple<Guid, DateTime> clickyTime = new Tuple<Guid, DateTime>(Guid.Empty, DateTime.MinValue);

        public void ClickyPlayTrack(Guid trackId)
        {
            if(clickyTime.Item1 == trackId && (DateTime.Now - clickyTime.Item2).TotalMilliseconds < 200)
            {
                PlayTrack(trackId);
            }

            clickyTime = new Tuple<Guid, DateTime>(trackId, DateTime.Now);
        }

        public void FadeAllMixerTracksOut()
        {
            if (IsPlaying)
            {
                foreach (var input in _mixerX.MixerInputs.ToList())
                {
                    EventHandler metho = null;
                    (input as FadeSampleProvider)?.FadeOutCompleted += metho = (s, e) =>
                    {
                        try
                        {
                            _mixerX.RemoveMixerInput(input);
                            (input as FadeSampleProvider)?.FadeOutCompleted -= metho;
                            (input as FadeSampleProvider)?.Dispose();
                            _logger.Debug($"Mixer input faded out and removed: {(input as FadeSampleProvider)._id}.  count: {_mixerX.MixerInputs.Count()}");
                        }
                        catch { }
                    };
                    (input as FadeSampleProvider)?.BeginFadeOut(TimeSpan.FromMilliseconds(200)); // Fade out over 200ms
                }
            }
            else
            {
                foreach (var input in _mixerX.MixerInputs.ToList())
                {
                    _mixerX.RemoveMixerInput(input);
                    (input as FadeSampleProvider)?.Dispose();
                    _logger.Debug($"Mixer input faded out and removed: {(input as FadeSampleProvider)._id}.  count: {_mixerX.MixerInputs.Count()}");
                }
            }
        }

        public async void PlayTrack(Guid trackId)
        {
            FadeAllMixerTracksOut();

            var track = GetListTrack(trackId);
            if (track != null)
            {
                try
                {
                    _currentTrack = await PlayingTrack.CreateAsync(track, 0);
                    _mixerX.AddMixerInput(_currentTrack.Provider);
                    _logger.Debug($"Mixer input ADDED: {_currentTrack.TrackID}.  count: {_mixerX.MixerInputs.Count()}");
                    try
                    {
                        _output?.Play();
                        IsPlaying = true;
                        CurrentPlayingTrackId = track.ID;
                    }
                    catch
                    {
                        IsPlaying = false;
                    }
                }
                catch (Exception ex)
                {
                    IsPlaying = false;
                    try
                    {
                        MessageBox.Show($"Failed to load track: {track.FileName}\n\nError: {ex.Message}",
                            "Playback Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    catch { }
                }
            }
        }

        public void AddTrack(Guid trackId)
        {
            var track = GetLibraryTrack(trackId);
            if (track != null)
            {
                Tracks.Add(track);
                NotifyOfChangeAsyncDelay(nameof(PlaylistTime));
            }
        }

        public void Minus(Guid trackId)
        {
            var track = GetListTrack(trackId);
            if (track != null)
            {
                track.GapSeconds--;
                NotifyOfChangeAsyncDelay(nameof(PlaylistTime));
            }
        }

        public void Plus(Guid trackId)
        {
            var track = GetListTrack(trackId);
            if (track != null)
            {
                track.GapSeconds++;
                NotifyOfChangeAsyncDelay(nameof(PlaylistTime));
            }
        }

        public void ReGenTrack(Guid trackId)
        {
            var track = GetListTrack(trackId);
            if (track != null)
            {
                track.ReGen();
            }
        }

        public void RemoveTrack(Guid trackId)
        {
            var track = GetListTrack(trackId);
            if (track != null)
            {
                Tracks.Remove(track);
                NotifyOfChangeAsyncDelay(nameof(PlaylistTime));
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

        private async void OnTimerTick(object? sender, EventArgs e)
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

                await MonitorPlayingEvents(progress);
            }
            else
            {
                TrackProgress = 0;
            }
        }

        bool Buffering = false;

        private async Task MonitorPlayingEvents(double progress)
        {
            // defensive checks
            if (_currentTrack == null)
            {
                CurrentTime = $"0:00";
                TotalTime = $"0:00";

                _progress = progress;
                return;
            }
            else
            {
                try
                {
                    CurrentTime = $"{(int)_currentTrack.CurrentTime.TotalMinutes}:{_currentTrack.CurrentTime.Seconds:D2}";
                    TotalTime = $"{(int)_currentTrack.TotalTime.TotalMinutes}:{_currentTrack.TotalTime.Seconds:D2}";
                }
                catch { }                
            }

            // if the progress is not moving (use epsilon to avoid float jitter), the current playing track is probably done
            if (Math.Abs(_progress - progress) < 0.00001)
            {
                // check if we are crossfading and need to switch to the buffer track
                if (_currentTrack.Track.GapSeconds < 0 &&
                    !_mixerX.MixerInputs.Any(x => x == _currentTrack.Provider) && _bufferTrack != null)
                {
                    try
                    {
                        // make buffer audible and swap
                        //_bufferTrack.Reader.Volume = 1.0f;
                        var old = _currentTrack;
                        _currentTrack = _bufferTrack;
                        //_output.Play();
                        IsPlaying = true;
                        SelectedTrack = _bufferTrack.Track;
                        CurrentPlayingTrackId = _bufferTrack.Track.ID;
                        _bufferTrack = null;

                        // dispose previous reader if it's no longer used
                        try { 
                            old.Dispose();
                            _logger.Debug($"Mixer input auto-removed F {_currentTrack.TrackID}.  count: {_mixerX.MixerInputs.Count()}");
                        } catch { }
                    }
                    catch
                    {
                        // if swap fails, ensure state is consistent
                        try { _output.Stop(); } catch { }
                        IsPlaying = false;
                        CurrentPlayingTrackId = null;
                        _bufferTrack = null;
                    }
                    return;
                }                
            }

            // prepare some timing helpers
            double remainingMs = (_currentTrack.Reader.TotalTime - _currentTrack.Reader.CurrentTime).TotalMilliseconds;

            // if the progress is over 90% (or very little remaining time) and there is no mixer input,
            // we ended a non-fading track, and need to load the next track
            if ((progress > 0.9 || remainingMs < 500) && !_mixerX.MixerInputs.Any())
            {
                if (_currentTrack.Track.GapSeconds > -1)
                {
                    var nextTrack = GetNextTrack();
                    if (nextTrack != null && !Buffering)
                    {
                        try
                        {
                            Buffering = true;
                            await PrepareBufferTrackAsync(nextTrack, _currentTrack.Track.GapSeconds);

                            // Verify buffer track was created successfully
                            if (_bufferTrack != null)
                            {
                                try
                                {
                                    var old = _currentTrack;
                                    _currentTrack = _bufferTrack;
                                    _output.Play();
                                    IsPlaying = true;
                                    SelectedTrack = nextTrack;
                                    CurrentPlayingTrackId = nextTrack.ID;

                                    Task.Run(() =>
                                    {
                                        Thread.Sleep((int)(((double)old.Track.GapSeconds + 0.5) * 1000.0)); // Wait for 1 second to ensure the track has started playing
                                        Buffering = false;
                                    });
                                    try { 
                                        old.Dispose();
                                        _logger.Debug($"Mixer input auto-removed 0 {_currentTrack.TrackID}.  count: {_mixerX.MixerInputs.Count()}");
                                    } catch { }
                                }
                                catch
                                {
                                    try { _output.Stop(); } catch { }
                                    IsPlaying = false;
                                    CurrentPlayingTrackId = null;
                                    Buffering = false;
                                }
                            }
                            else
                            {
                                Buffering = false;
                                // Buffer track creation failed, stop playback
                                try { _output.Stop(); } catch { }
                                IsPlaying = false;
                                CurrentPlayingTrackId = null;
                            }
                        }
                        catch
                        {
                            // Track loading failed, stop playback gracefully
                            try { _output.Stop(); } catch { }
                            IsPlaying = false;
                            CurrentPlayingTrackId = null;
                            Buffering = false;
                        }
                    }
                    else
                    {
                        try { _output.Stop(); } catch { }
                        IsPlaying = false;
                        CurrentPlayingTrackId = null;
                        Buffering = false;
                    }
                }
            }

            // if the current track has a negative gap (indicating a crossfade) and progress is greater than 50%
            else if (_currentTrack.Track.GapSeconds < 0 && progress > 0.5)
            {
                // check if it's time to kick off the crossfade, if it's not already set
                if (!_currentTrack.fadingOut)
                {
                    double triggerMs = Math.Abs(_currentTrack.Track.GapSeconds) * 1000.0;
                    if (remainingMs < triggerMs)
                    {
                        var nextTrack = GetNextTrack();
                        if (nextTrack != null)
                        {
                            try
                            {
                                _currentTrack.BeginFadeOut();
                                _currentTrack.fadingOut = true;
                                _bufferTrack = await PlayingTrack.CreateAsync(nextTrack, 0);
                                _bufferTrack.Reader.Volume = _bufferTrack.Track.FadeInOnCross? 0.0f : 1.0f; // Start the next track muted for fade-in
                                _mixerX.AddMixerInput(_bufferTrack.Provider);
                                _logger.Debug($"Mixer input ADDED: {_bufferTrack.TrackID}.  count: {_mixerX.MixerInputs.Count()}");
                            }
                            catch
                            {
                                // failed to prepare buffer, reset flags
                                _currentTrack.fadingOut = false;
                                if (_bufferTrack != null)
                                {
                                    try { _bufferTrack.Dispose(); } catch { }
                                    _bufferTrack = null;
                                }
                            }
                        }
                    }
                }
            }

            // update our progress tracker for the next tick
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

        private PlaylistTrack? GetPreviousTrack()
        {
            if (_currentTrack == null)
                return Tracks.FirstOrDefault();

            var result = Tracks.IndexOf(_currentTrack.Track);
            if (result > 0)
            {
                return Tracks[result - 1];
            }

            return null;
        }

        public async void SkipForward()
        {
            var nextTrack = GetNextTrack();
            if (nextTrack != null)
            {
                try
                {
                    FadeAllMixerTracksOut();

                    _selectionID = nextTrack.ID;
                    SelectedTrack = nextTrack;
                    _currentTrack = await PlayingTrack.CreateAsync(nextTrack, 0);
                    _mixerX.AddMixerInput(_currentTrack.Provider);
                    _logger.Debug($"Mixer input ADDED: {_currentTrack.TrackID}.  count: {_mixerX.MixerInputs.Count()}");
                    CurrentPlayingTrackId = nextTrack.ID;

                    if (IsPlaying)
                    {
                        try
                        {
                            _output?.Play();
                        }
                        catch
                        {
                            IsPlaying = false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    IsPlaying = false;
                    try
                    {
                        MessageBox.Show($"Failed to skip to track: {nextTrack.FileName}\n\nError: {ex.Message}", 
                            "Playback Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    catch { }
                }
            }
        }

        public async void SkipBackward()
        {
            var previousTrack = GetPreviousTrack();
            if (previousTrack != null)
            {
                try
                {
                    FadeAllMixerTracksOut();

                    _selectionID = previousTrack.ID;
                    SelectedTrack = previousTrack;
                    _currentTrack = await PlayingTrack.CreateAsync(previousTrack, 0);
                    _mixerX.AddMixerInput(_currentTrack.Provider);
                    _logger.Debug($"Mixer input ADDED: {_currentTrack.TrackID}.  count: {_mixerX.MixerInputs.Count()}");
                    CurrentPlayingTrackId = previousTrack.ID;

                    if (IsPlaying)
                    {
                        try
                        {
                            _output?.Play();
                        }
                        catch
                        {
                            IsPlaying = false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    IsPlaying = false;
                    try
                    {
                        MessageBox.Show($"Failed to skip to track: {previousTrack.FileName}\n\nError: {ex.Message}", 
                            "Playback Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    catch { }
                }
            }
        }

        private async Task PrepareBufferTrackAsync(PlaylistTrack track, int offset)
        {
            try
            {
                _bufferTrack = await PlayingTrack.CreateAsync(track, offset);
                _mixerX.AddMixerInput(_bufferTrack.Provider);
                _logger.Debug($"Mixer input ADDED: {_bufferTrack.TrackID}.  count: {_mixerX.MixerInputs.Count()}");
            }
            catch (Exception e)
            {
                _bufferTrack = null;
            }
        }

        private string localAppData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DJCMS");

        private async void AutoLoad()
        {
            
            var supportedExtensions = new[] { ".mp3", ".wav", ".m4a", ".flac", ".aac", ".wma", ".ogg" };
            var supportedPL_Extensions = new[] { ".json" };

            //library
            var libraryFolderPath = @"D:\Music\DJing\717_backup";
            var fileArray2 = Directory.GetFiles(libraryFolderPath)
                .Where(file => supportedExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                .OrderBy(file => file)
                .ToArray();
            LoadLibrary(fileArray2);

            //tracks
            Tracks = await LoadPlaylistFile($"{localAppData}\\playlist.json");

            //playlists
            var fileArray1 = Directory.GetFiles(localAppData)
               .Where(file => supportedPL_Extensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
               .OrderBy(file => file)
               .ToArray();
            LoadPlaylistFiles(fileArray1);

            NotifyOfChangeAsyncDelay(nameof(PlaylistTime));
        }

        private async void AutoLoad2()
        {
            var localAppData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DJCMS");


            var supportedPL_Extensions = new[] { ".json" };
            var fileArray1 = Directory.GetFiles(localAppData)
                .Where(file => supportedPL_Extensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                .OrderBy(file => file)
                .ToArray();
            LoadPlaylistFiles(fileArray1);


            var libraryFolderPath = @"C:\Users\christian.hahn\Downloads";

            var supportedExtensions = new[] { ".mp3", ".wav", ".m4a", ".flac", ".aac", ".wma", ".ogg" };
            var fileArray2 = Directory.GetFiles(libraryFolderPath)
                .Where(file => supportedExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                .OrderBy(file => file)
                .ToArray();

            LoadLibrary(fileArray2);

            Tracks = await LoadPlaylistFile($"{localAppData}\\playlist.json");

            NotifyOfChangeAsyncDelay(nameof(PlaylistTime));
        }

        public void LoadPlaylistFiles(string[] files)
        {
            PlaylistLibrary = new ObservableCollection<PlaylistFile>();
            foreach (var file in files)
            {
                PlaylistLibrary.Add(new PlaylistFile { FilePath = file });
            }
        }

        public void LoadFiles(string[] files)
        {
            foreach (var file in files)
            {
                Tracks.Add(new PlaylistTrack { FilePath = file, GapSeconds = 0 });
            }

            NotifyOfChangeAsyncDelay(nameof(PlaylistTime));
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

            NotifyOfChangeAsyncDelay(nameof(PlaylistTime));
        }

        private PlaylistTrack? GetListTrack(Guid id)
        {
            return Tracks.FirstOrDefault(t => t.ID == id);
        }

        private PlaylistFile? GetPlaylistFromId(Guid id)
        {
            return PlaylistLibrary?.FirstOrDefault(t => t.ID == id);
        }

        private PlaylistTrack? GetLibraryTrack(Guid id)
        {
            return LibraryFolder?.FirstOrDefault(t => t.ID == id);
        }

        // TODO: Implement saving/loading playlists. Wired to the view via Caliburn Micro actions.
        public async Task SavePlaylist()
        {
            try
            {
                await SavePlaylistAsync(Tracks);
            }
            catch (Exception ex)
            {
                try { MessageBox.Show($"Failed to save playlist: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); } catch { }
            }
        }

        public async Task LoadPlaylist()
        {
            try
            {
                Stop();

                _currentTrack?.Dispose();
                _bufferTrack?.Dispose();

                _currentTrack = null;
                _bufferTrack = null;
                _selectedTrack = null;
                _selectionID = null;
                _trackProgress = 0;
                Tracks = await LoadPlaylistAsync() ?? new ObservableCollection<PlaylistTrack>();
            }
            catch (Exception ex)
            {
                try { MessageBox.Show($"Failed to load playlist: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); } catch { }
            }

            NotifyOfChangeAsyncDelay(nameof(PlaylistTime));
        }

        public static async Task SavePlaylistAsync(
    ObservableCollection<PlaylistTrack> tracks)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "DJ Playlist (*.json)|*.json|All Files (*.*)|*.*",
                DefaultExt = ".json",
                AddExtension = true,
                FileName = "playlist.json",
                DefaultDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DJCMS")
            };

            if (dialog.ShowDialog() != true)
                return;

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            string json = JsonSerializer.Serialize(tracks, options);

            try
            {
                await File.WriteAllTextAsync(dialog.FileName, json);
            }
            catch (Exception ex)
            {
                try { MessageBox.Show($"Unable to save playlist: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); } catch { }
            }
        }

        public static async Task<ObservableCollection<PlaylistTrack>?> LoadPlaylistAsync()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "DJ Playlist (*.json)|*.json|All Files (*.*)|*.*",
                DefaultDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DJCMS")
            };

            if (dialog.ShowDialog() != true)
                return null;

            try
            {
                var tracks = await LoadPlaylistFile(dialog.FileName);

                if(tracks != null && tracks.Any())
                    return tracks;
                else
                    return null;
            }
            catch (Exception ex)
            {
                try { MessageBox.Show($"Unable to load playlist: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); } catch { }
                return null;
            }
        }

        public async void LoadAPlaylistFile(string filename)
        {
            var tracks = await LoadPlaylistFile(filename);

            if (tracks != null && tracks.Any())
                Tracks = tracks;
        }

        public static async Task<ObservableCollection<PlaylistTrack>> LoadPlaylistFile(string filePath)
        {
            try
            {
                string json = await File.ReadAllTextAsync(filePath);
                var tracks =
                    JsonSerializer.Deserialize<ObservableCollection<PlaylistTrack>>(json);
                return tracks ?? new ObservableCollection<PlaylistTrack>();
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        protected override Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
        {
            if (close)
            {
                // Stop timer FIRST to prevent race conditions
                _timer.Stop();

                try
                {
                    _output.Stop();
                }
                catch { }

                try
                {
                    foreach (var input in _mixerX.MixerInputs.ToList())
                    {
                        (input as FadeSampleProvider)?.Dispose();
                    }
                    _mixerX.RemoveAllMixerInputs();
                    _logger.Debug($"Mixer inputs CLEARED.  count: {_mixerX.MixerInputs.Count()}");
                }
                catch { }

                try
                {
                    _currentTrack?.Dispose();
                }
                catch { }

                try
                {
                    _bufferTrack?.Dispose();
                }
                catch { }

                try
                {
                    _output.Dispose();
                }
                catch { }
            }
            return base.OnDeactivateAsync(close, cancellationToken);
        }

        public async void NotifyOfChangeAsyncDelay(string propertyName)
        {
            await Task.Delay(500);
            NotifyOfPropertyChange(propertyName);
        }

        public string PlaylistTime
        {
            get
            {
                var totalSeconds = Tracks.Sum(t => t.TotalSeconds + t.GapSeconds);
                var minutes = totalSeconds / 60;
                var seconds = totalSeconds % 60;
                return $"{minutes}:{seconds:D2}";
            }
        }
    }
}

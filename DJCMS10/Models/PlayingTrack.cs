using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.IO;

namespace DJCMS.Models;

public sealed class PlayingTrack : IDisposable
{    
    public AudioFileReader Reader { get; }
    public ISampleProvider Provider { get; }
    private FadeSampleProvider _fadeSampleProvider { get; }
    public Guid TrackID { get; }
    public PlaylistTrack Track { get; }

    public bool fadingOut = false;

    public Guid LoggyID => _fadeSampleProvider.loggyID;

    private PlayingTrack(PlaylistTrack track, int offset)
    {
        if (string.IsNullOrEmpty(track.FilePath))
            throw new FileNotFoundException("Track file path is null or empty.", track.FilePath);

        if (!File.Exists(track.FilePath))
            throw new FileNotFoundException($"Audio file not found: {track.FilePath}", track.FilePath);

        try
        {
            Reader = new AudioFileReader(track.FilePath);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load audio file: {track.FilePath}", ex);
        }

        TrackID = track.ID;
        Track = track;

        ISampleProvider provider1 = Reader;

        if (provider1.WaveFormat.Channels == 1)
        {
            provider1 = new MonoToStereoSampleProvider(provider1);
        }

        if (provider1.WaveFormat.SampleRate != 44100)
        {
            provider1 = new WdlResamplingSampleProvider(provider1, 44100);
        }

        var provider = new OffsetSampleProvider(provider1)
        {
            DelayBy = TimeSpan.FromSeconds(offset)
        };

        _fadeSampleProvider = new FadeSampleProvider(provider,Reader,TrackID);
        Provider = _fadeSampleProvider;
    }

    public event EventHandler? FadeOutCompleted;
    public void BeginFadeOut(TimeSpan? duration = null)
    {
        TimeSpan _duration = duration ?? TimeSpan.FromSeconds(Math.Max(Math.Abs(Track.GapSeconds) - 0.5,0)); // Default to 5 seconds if no duration is provided

        _fadeSampleProvider.FadeOutCompleted += _fadeSampleProvider_FadeOutCompleted1;
        _fadeSampleProvider.BeginFadeOut(_duration);
    }

    private void _fadeSampleProvider_FadeOutCompleted1(object? sender, EventArgs e)
    {
        this.FadeOutCompleted?.Invoke(this, EventArgs.Empty);
        _fadeSampleProvider.FadeOutCompleted -= _fadeSampleProvider_FadeOutCompleted1;
    }

    private void _fadeSampleProvider_FadeOutCompleted(object? sender, EventArgs e)
    {
        throw new NotImplementedException();
    }

    // Create PlayingTrack off the UI thread to avoid blocking the UI when opening files / resampling
    public static Task<PlayingTrack> CreateAsync(PlaylistTrack track, int offset)
    {
        return Task.Run(() => new PlayingTrack(track, offset));
    }

    public TimeSpan TotalTime
    {
        get
        {
            try
            {
                return Reader?.TotalTime ?? TimeSpan.MinValue;
            }
            catch
            {
                return TimeSpan.MinValue;
            }
        }
    } 
    public TimeSpan CurrentTime
    {
        get
        {
            try
            {
                if(!_disposed && !_fadeSampleProvider._disposed)
                {
                    return Reader?.CurrentTime ?? TimeSpan.MinValue;
                }
                else
                {
                    return TimeSpan.MinValue;
                }
            }
            catch
            {
                return TimeSpan.MinValue;
            }
        }
    }
    

    public void Seek(TimeSpan time)
    {
        try
        {
            if (time >= TimeSpan.Zero && time <= Reader.TotalTime)
            {
                Reader.CurrentTime = time;
            }
        }
        catch
        {
            // Seeking failed, ignore to prevent crash
        }
    }

    bool _disposed;
    public void Dispose()
    {
        try
        {
            if (_disposed) return;
            // Dispose the fade provider which owns the reader. This centralizes ownership
            // and avoids direct reader disposal scattered across the app.
            _fadeSampleProvider?.Dispose();
            _disposed = true;
        }
        catch
        {
            // Ignore exceptions during dispose
        }
    }
}

public class FadeSampleProvider : ISampleProvider, IDisposable
{
    private readonly ISampleProvider source;
    public WaveFormat WaveFormat => source.WaveFormat;

    float currentGain = 0.0f;
    float fadeStep = 0.0f;
    int samplesRemaining = 0; // samples left in fade (per channel sample count)
    public event EventHandler? FadeOutCompleted;

    IDisposable _reader;

    public bool FadingOut = false;

    bool fadeIn = true;

    public Guid loggyID = Guid.NewGuid();

    public FadeSampleProvider(ISampleProvider source, IDisposable reader, Guid id)
    {
        this.source = source;
        _reader = reader;
    }

    // begin a short fade to 0 over duration; safe to call from UI thread
    public void BeginFadeOut(TimeSpan duration)
    {
        if (!FadingOut)
        {
            FadingOut = true;
            int sampleRate = WaveFormat.SampleRate;
            int channels = WaveFormat.Channels;
            int fadeSamples = Math.Max(1, (int)(duration.TotalSeconds * sampleRate) * channels);
            fadeStep = -currentGain / fadeSamples;
            samplesRemaining = fadeSamples;
            if (fadeSamples == 0)
            {
                currentGain = 0;
                FadeOutCompleted?.Invoke(this, EventArgs.Empty);
            }
        }       
    }

    public int Read(float[] buffer, int offset, int count)
    {
        int read = source.Read(buffer, offset, count);
        if (read == 0) return 0;

        for (int n = 0; n < read; n++)
        {
            // apply current gain to each float sample
            buffer[offset + n] *= currentGain;

            if (FadingOut && samplesRemaining > 0)
            {
                currentGain += fadeStep;
                samplesRemaining--;
                if (samplesRemaining == 0)
                {
                    currentGain = Math.Max(0, currentGain);
                    FadeOutCompleted?.Invoke(this, EventArgs.Empty);
                }
            }
            else if (fadeIn && currentGain < 1.0f)
            {
                // simple fade in logic
                currentGain += 0.01f; // adjust this value for faster/slower fade in
                if (currentGain > 1.0f)
                {
                    currentGain = 1.0f;
                    fadeIn = false; // fade in complete
                }
            }
        }
        return read;
    }

    public bool _disposed;
    public void Dispose()
    {
        try
        {
            if (_disposed) return;
            this._reader?.Dispose();
            _disposed = true;
        }
        catch
        {
            
        }
    }
}

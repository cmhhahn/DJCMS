using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.IO;

namespace DJCMS.Models;

public sealed class PlayingTrack
{
    public AudioFileReader Reader { get; }
    public ISampleProvider Provider { get; }
    public Guid TrackID { get; }
    public PlaylistTrack Track { get; }

    public bool fadingIn = false;
    public bool fadingOut = false;

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

        Provider = new FadeSampleProvider(provider);
    }

    // Create PlayingTrack off the UI thread to avoid blocking the UI when opening files / resampling
    public static Task<PlayingTrack> CreateAsync(PlaylistTrack track, int offset)
    {
        return Task.Run(() => new PlayingTrack(track, offset));
    }

    public TimeSpan TotalTime => Reader.TotalTime;
    public TimeSpan CurrentTime => Reader.CurrentTime;

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
}

public class FadeSampleProvider : ISampleProvider
{
    private readonly ISampleProvider source;
    public WaveFormat WaveFormat => source.WaveFormat;

    float currentGain = 0.0f;
    float fadeStep = 0.0f;
    int samplesRemaining = 0; // samples left in fade (per channel sample count)
    public event EventHandler? FadeOutCompleted;

    bool fadeIn = true;

    public FadeSampleProvider(ISampleProvider source)
    {
        this.source = source;
    }

    // begin a short fade to 0 over duration; safe to call from UI thread
    public void BeginFadeOut(TimeSpan duration)
    {
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

    public int Read(float[] buffer, int offset, int count)
    {
        int read = source.Read(buffer, offset, count);
        if (read == 0) return 0;

        for (int n = 0; n < read; n++)
        {
            // apply current gain to each float sample
            buffer[offset + n] *= currentGain;

            if (samplesRemaining > 0)
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
}

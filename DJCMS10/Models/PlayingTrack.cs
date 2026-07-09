using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.IO;

namespace DJCMS.Models
{
    public sealed class PlayingTrack
    {
        public AudioFileReader Reader { get; }
        public ISampleProvider Provider { get; }
        public Guid TrackID { get; }
        public PlaylistTrack Track { get; }

        public bool fadingIn = false;
        public bool fadingOut = false;

        public PlayingTrack(PlaylistTrack track, int offset)
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

            Provider = provider;
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
}

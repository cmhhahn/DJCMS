using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;

namespace DJCMS.Models
{
    public sealed class PlayingTrack
    {
        public AudioFileReader Reader { get; }
        public ISampleProvider Provider { get; }
        public Guid TrackID { get; }
        public PlaylistTrack Track { get; }

        public PlayingTrack(PlaylistTrack track, int offset)
        {
            Reader = new AudioFileReader(track.FilePath);
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
            Reader.CurrentTime = time;
        }
    }
}

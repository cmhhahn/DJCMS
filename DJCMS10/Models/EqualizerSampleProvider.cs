using NAudio.Dsp;
using NAudio.Wave;

namespace DJCMS.Models
{
    public class EqualizerSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider _source;
        private readonly BiQuadFilter[,] _filters;
        private readonly float[] _bandGains;
        private readonly int _channels;
        private readonly int _sampleRate;

        public WaveFormat WaveFormat => _source.WaveFormat;

        public EqualizerSampleProvider(ISampleProvider source)
        {
            _source = source;
            _channels = source.WaveFormat.Channels;
            _sampleRate = source.WaveFormat.SampleRate;

            // 10 bands, 2 channels (stereo)
            _filters = new BiQuadFilter[10, _channels];
            _bandGains = new float[10];

            // Initialize all gains to 0 dB (no change)
            for (int i = 0; i < 10; i++)
            {
                _bandGains[i] = 0;
            }

            UpdateFilters();
        }

        public void SetGain(int band, float gainDb)
        {
            if (band < 0 || band >= 10)
                return;

            _bandGains[band] = gainDb;
            UpdateBandFilter(band);
        }

        public float GetGain(int band)
        {
            if (band < 0 || band >= 10)
                return 0;

            return _bandGains[band];
        }

        private void UpdateFilters()
        {
            for (int i = 0; i < 10; i++)
            {
                UpdateBandFilter(i);
            }
        }

        private void UpdateBandFilter(int band)
        {
            float centerFrequency = GetCenterFrequency(band);
            float bandwidth = GetBandwidth(band);
            float gainDb = _bandGains[band];

            for (int channel = 0; channel < _channels; channel++)
            {
                if (Math.Abs(gainDb) < 0.01f)
                {
                    // No gain change, use pass-through
                    _filters[band, channel] = BiQuadFilter.PeakingEQ(_sampleRate, centerFrequency, bandwidth, 0);
                }
                else
                {
                    _filters[band, channel] = BiQuadFilter.PeakingEQ(_sampleRate, centerFrequency, bandwidth, gainDb);
                }
            }
        }

        private float GetCenterFrequency(int band)
        {
            return band switch
            {
                0 => 31f,
                1 => 62f,
                2 => 125f,
                3 => 250f,
                4 => 500f,
                5 => 1000f,
                6 => 2000f,
                7 => 4000f,
                8 => 8000f,
                9 => 16000f,
                _ => 1000f
            };
        }

        private float GetBandwidth(int band)
        {
            // Q factor for peaking EQ (lower Q = wider bandwidth)
            // Typical values: 0.5 to 2.0
            return 0.8f;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int samplesRead = _source.Read(buffer, offset, count);

            // Bounds check
            if (samplesRead <= 0 || offset + samplesRead > buffer.Length)
                return samplesRead;

            // Apply each band's filter
            for (int band = 0; band < 10; band++)
            {
                // Skip if gain is essentially zero
                if (Math.Abs(_bandGains[band]) < 0.01f)
                    continue;

                for (int i = 0; i < samplesRead; i++)
                {
                    int bufferIndex = offset + i;
                    if (bufferIndex >= buffer.Length)
                        break;

                    int channel = i % _channels;
                    buffer[bufferIndex] = _filters[band, channel].Transform(buffer[bufferIndex]);
                }
            }

            return samplesRead;
        }
    }
}

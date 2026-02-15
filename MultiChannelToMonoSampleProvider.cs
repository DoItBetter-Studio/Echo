using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NAudio.Wave;

namespace Echo
{
	class MultiChannelToMonoSampleProvider : ISampleProvider
	{
		private readonly ISampleProvider source;
		private readonly int channels;
		private readonly float[] buffer;

		public MultiChannelToMonoSampleProvider(ISampleProvider source)
		{
			this.source = source;
			channels = source.WaveFormat.Channels;
			buffer = new float[4096 * channels];
			WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(source.WaveFormat.SampleRate, 1);
		}

		public WaveFormat WaveFormat { get; }

		public int Read(float[] output, int offset, int count)
		{
			int samplesNeeded = count * channels;
			int read = source.Read(buffer, 0, samplesNeeded);

			int frames = read / channels;

			for (int i = 0; i < frames; i++)
			{
				float sum = 0f;
				for (int c = 0; c < channels; c++)
					sum += buffer[i * channels + c];

				output[offset + i] = sum / channels;
			}

			return frames;
		}
	}
}

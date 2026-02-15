using System;
using System.Collections.Generic;

using NAudio.Wave;

namespace Echo
{
	class CachedLoopWaveProvider : IWaveProvider
	{
		private readonly float[] buffer;
		private readonly int loopStart;
		private readonly int loopEnd;
		private int position;

		public WaveFormat WaveFormat { get; }

		public CachedLoopWaveProvider(ISampleProvider source, float loopStartNorm, float loopEndNorm)
		{
			// Store as 16-bit PCM wave format
			WaveFormat = new WaveFormat(source.WaveFormat.SampleRate, 16, source.WaveFormat.Channels);

			var temp = new List<float>(1024);
			float[] readBuf = new float[4096];

			int read;
			while ((read = source.Read(readBuf, 0, readBuf.Length)) > 0)
			{
				for (int i = 0; i < read; i++)
					temp.Add(readBuf[i]);
			}

			if (temp.Count == 0)
				throw new InvalidOperationException("No audio data to loop");

			buffer = temp.ToArray();

			// Convert normalized positions to sample indices
			loopStart = (int) (loopStartNorm * buffer.Length);
			loopEnd = (int) (loopEndNorm * buffer.Length);

			// Ensure valid range
			loopStart = Math.Max(0, Math.Min(loopStart, buffer.Length - 1));
			loopEnd = Math.Max(loopStart + 1, Math.Min(loopEnd, buffer.Length));

			// Start playback at loop start
			position = loopStart;
		}

		public int Read(byte[] output, int offset, int count)
		{
			// Safety check
			if (count == 0 || buffer.Length == 0)
				return 0;

			int samplesNeeded = count / 2; // 16-bit = 2 bytes per sample
			int samplesWritten = 0;
			int byteOffset = offset;

			while (samplesWritten < samplesNeeded)
			{
				int available = loopEnd - position;

				// Safety check - prevent infinite loop
				if (available <= 0)
				{
					position = loopStart;
					available = loopEnd - position;

					if (available <= 0) // Still bad? Bail out.
						break;
				}

				int toWrite = Math.Min(samplesNeeded - samplesWritten, available);

				for (int i = 0; i < toWrite; i++)
				{
					// Convert float (-1.0 to 1.0) to short (-32768 to 32767)
					float sample = buffer[position];
					short pcm = (short) Math.Clamp(sample * 32767f, -32768, 32767);

					// Write as little-endian 16-bit
					output[byteOffset++] = (byte) (pcm & 0xFF);
					output[byteOffset++] = (byte) (pcm >> 8);

					position++;
				}

				samplesWritten += toWrite;

				// Loop back to loop start when we hit loop end
				if (position >= loopEnd)
					position = loopStart;
			}

			return samplesWritten * 2; // Return bytes written
		}
	}
}

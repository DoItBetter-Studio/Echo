using System;
using System.Collections.Generic;
using System.Diagnostics;

using NAudio.Wave;

namespace Echo
{
	class CachedLoopSampleProvider : ISampleProvider
	{
		private readonly float[] buffer;
		private readonly int loopStart;
		private readonly int loopEnd;
		private int position;

		public WaveFormat WaveFormat { get; }

		public CachedLoopSampleProvider(ISampleProvider source, float loopStartNorm, float loopEndNorm)
		{
			WaveFormat = source.WaveFormat;

			Trace.WriteLine($"Source WaveFormat: {WaveFormat.Encoding}, {WaveFormat.BitsPerSample} bits, {WaveFormat.SampleRate}Hz");

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

			Trace.WriteLine($"Loop provider: buffer={buffer.Length}, loopStart={loopStart}, loopEnd={loopEnd}");

			// Start playback at loop start
			position = loopStart;
		}

		public int Read(float[] output, int offset, int count)
		{
			int written = 0;

			while (written < count)
			{
				int available = loopEnd - position;
				int toCopy = Math.Min(count - written, available);

				try
				{
					Array.Copy(buffer, position, output, offset + written, toCopy);
				}
				catch (Exception ex)
				{
					Trace.WriteLine($"Error: output is {output.GetType()}, buffer is {buffer.GetType()}");
					Trace.WriteLine($"position={position}, offset={offset}, written={written}, toCopy={toCopy}");
					throw;
				}

				position += toCopy;
				written += toCopy;

				if (position >= loopEnd)
					position = loopStart;
			}

			return written;
		}
	}
}

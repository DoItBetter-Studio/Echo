using System;
using System.IO;
using System.Text;

namespace Echo.Data
{
	/// <summary>
	/// Minimal WAV decoder. Produces signed 8-bit mono PCM samples matching
	/// what the Glyphborn runtime consumes directly.
	/// Supports: 8-bit mono/stereo, 16-bit mono/stereo input (downmixed + converted).
	/// No external dependencies.
	/// </summary>
	public static class WavDecoder
	{
		public static sbyte[] Decode(string path, out int sampleRate)
		{
			using var fs = File.OpenRead(path);
			using var br = new BinaryReader(fs);

			// RIFF header
			string riff = Encoding.ASCII.GetString(br.ReadBytes(4));
			if (riff != "RIFF") throw new InvalidDataException("Not a valid WAV file (missing RIFF header).");
			br.ReadUInt32(); // chunk size
			string wave = Encoding.ASCII.GetString(br.ReadBytes(4));
			if (wave != "WAVE") throw new InvalidDataException("Not a valid WAV file (missing WAVE header).");

			// Parse chunks until we find fmt and data
			ushort audioFormat = 0, channels = 0, bitsPerSample = 0;
			sampleRate = 44100;
			byte[]? pcmData = null;

			while (fs.Position < fs.Length - 8)
			{
				string chunkId	= Encoding.ASCII.GetString(br.ReadBytes(4));
				uint chunkSize	= br.ReadUInt32();
				long chunkEnd	= fs.Position + chunkSize;

				switch (chunkId)
				{
					case "fmt ":
						audioFormat = br.ReadUInt16();
						channels = br.ReadUInt16();
						sampleRate = (int)br.ReadUInt32();
						br.ReadUInt32(); // byte rate
						br.ReadUInt16(); // block align
						bitsPerSample = br.ReadUInt16();
						break;

					case "data":
						pcmData = br.ReadBytes((int)chunkSize);
						break;
				}

				// Seek past any extra chunk bytes
				if (fs.Position < chunkEnd)
					fs.Seek(chunkEnd, SeekOrigin.Begin);
			}

			if (pcmData == null) throw new InvalidDataException("Not a valid WAV file (missing data chunk).");
			if (audioFormat != 1) throw new InvalidDataException("Only PCM WAV supported (no compression).");

			return ConvertToS8Mono(pcmData, channels, bitsPerSample);
		}

		private static sbyte[] ConvertToS8Mono(byte[] raw, ushort channels, ushort bitsPerSample)
		{
			int bytesPerSample	= bitsPerSample / 8;
			int frameCount		= raw.Length / (bytesPerSample * channels);
			sbyte[] result		= new sbyte[frameCount];

			for (int i = 0; i < frameCount; i++)
			{
				int offset = i * bytesPerSample * channels;

				// Mix down channels to mono as 32-bit accumulator
				int mixed = 0;
				for (int ch = 0; ch < channels; ch++)
				{
					int chOffset = offset + ch * bytesPerSample;
					if (bitsPerSample == 8)
					{
						// WAV 8-bit is unsigned (0..255), centre at 128
						mixed += (int)raw[chOffset] - 128;
					}
					else if (bitsPerSample == 16)
					{
						// WAV 16-bit is signed little-endian
						short s16 = (short)(raw[chOffset] | (raw[chOffset + 1] << 8));
						// Scale to s8 range: divide by 256
						mixed += s16 >> 8;
					}
				}

				// Average channels, clamp to s8
				mixed /= channels;
				result[i] = (sbyte)Math.Clamp(mixed, -128, 127);
			}

			return result;
		}
	}
}

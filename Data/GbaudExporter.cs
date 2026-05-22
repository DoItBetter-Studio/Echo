using System;
using System.IO;

namespace Echo.Data
{
	/// <summary>
	/// Writes a .gbaud file matching the GbaudHeader struct the runtime reads:
	///
	///   char     magic[4]       'G','B','A','U'
	///   uint16   version        1
	///   uint32   sample_rate
	///   uint8    channels       1 (mono)
	///   uint8    bit_depth      8
	///   uint8    flags          bit 0 = loop enabled
	///   uint8    _pad
	///   uint32   sample_count   trimmed length
	///   uint32   trim_start
	///   uint32   trim_end
	///   uint32   loop_start     relative to trim_start
	///   uint32   loop_end       relative to trim_start
	///   uint32   data_offset    = 36
	///   [raw s8 PCM from trim_start..trim_end inclusive]
	/// </summary>
	public static class GbaudExporter
	{
		private const uint DATA_OFFSET = 38;
		private const ushort VERSION = 1;

		public static void Write(AudioDocument doc, string outputPath)
		{
			int start = doc.TrimStart;
			int end = doc.TrimEnd;
			int sampleCount = end - start + 1;

			// Loop points are stored relative to trim_start so the runtime
			// can index directly into the PCM block it receives
			uint loopStart = (uint)Math.Max(0, doc.LoopStart - start);
			uint loopEnd = (uint)Math.Min(sampleCount - 1, doc.LoopEnd - start);

			byte flags = (byte)(doc.LoopEnabled ? 0x01 : 0x00);

			using var fs = File.Create(outputPath);
			using var bw = new BinaryWriter(fs);

			// Magic
			bw.Write((byte)'G');
			bw.Write((byte)'B');
			bw.Write((byte)'A');
			bw.Write((byte)'U');

			// version (u16)
			bw.Write(VERSION);

			// sample_rate (u32)
			bw.Write((uint)doc.SampleRate);

			// channels (u8)
			bw.Write((byte)1);

			// bit_depth (u8)
			bw.Write((byte)8);

			// flags (u8)
			bw.Write(flags);

			// _pad (u8)
			bw.Write((byte)0);

			// sample_count (u32)
			bw.Write((uint)sampleCount);

			// trim_start (u32) — absolute, for reference
			bw.Write((uint)start);

			// trim_end (u32) — absolute, for reference
			bw.Write((uint)end);

			// loop_start (u32) — relative to trim_start
			bw.Write(loopStart);

			// loop_end (u32) — relative to trim_start
			bw.Write(loopEnd);

			// data_offset (u32)
			bw.Write(DATA_OFFSET);

			// Verify we're at exactly DATA_OFFSET bytes
			System.Diagnostics.Debug.Assert(fs.Position == DATA_OFFSET,
				$"Header size mismatch: expected {DATA_OFFSET}, got {fs.Position}");

			// Raw s8 PCM — write as raw bytes (cast sbyte→byte, same bits)
			for (int i = start; i <= end; i++)
				bw.Write((byte)doc.Samples[i]);
		}
	}
}
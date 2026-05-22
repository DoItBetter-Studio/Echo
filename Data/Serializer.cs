using Echo.Editor;
using System;
using System.IO;
using System.Security.AccessControl;
using System.Text;

namespace Echo.Data
{
	/// <summary>
	/// Reads and writes .gbaud editor project files to assets/audio/.
	/// These are NOT runtime binaries — they store the editor state only.
	///
	/// Binary layout:
	///   u32   magic        'E','A','U','D'  (0x44554145)
	///   u16   version      1
	///   u16   pathLen
	///   u8[]  sourcePath   UTF-8, pathLen bytes
	///   u8    flags        AudioFlags
	///   i32   trimStart    sample index
	///   i32   trimEnd      sample index
	///   i32   loopStart    sample index
	///   i32   loopEnd      sample index
	/// </summary>
	public static class Serializer
	{
		private const uint MAGIC = 0x44554145; // EAUD
		private const ushort VERSION = 1;

		public static void Write(string name, AudioDocument doc)
		{
			string path = Path.Combine(EditorPaths.AssetsAudio, $"{name}.gbaud");

			Directory.CreateDirectory(EditorPaths.AssetsAudio);

			using var fs = File.Create(path);
			using var bw = new BinaryWriter(fs);

			bw.Write(MAGIC);
			bw.Write(VERSION);

			byte[] pathBytes = Encoding.UTF8.GetBytes(doc.SourcePath);
			bw.Write((ushort)pathBytes.Length);
			bw.Write(pathBytes);

			bw.Write((byte)doc.Flags);

			bw.Write(doc.TrimStart);
			bw.Write(doc.TrimEnd);
			bw.Write(doc.LoopStart);
			bw.Write(doc.LoopEnd);
		}

		public static AudioDocument Read(string name)
		{
			string path = Path.Combine(EditorPaths.AssetsAudio, $"{name}.gbaud");

			using var fs = File.OpenRead(path);
			using var br = new BinaryReader(fs);

			uint magic = br.ReadUInt32();
			if (magic != MAGIC)
				throw new InvalidDataException($"Invalid .gbaud editor file (bad magic in '{name}')");

			ushort version = br.ReadUInt16();
			if (version != VERSION)
				throw new InvalidDataException($"Unsupported .gbaud version {version} in '{name}'");

			ushort pathLen = br.ReadUInt16();
			string sourcePath = Encoding.UTF8.GetString(br.ReadBytes(pathLen));

			var flags = (AudioFlags)br.ReadByte();

			int trimStart = br.ReadInt32();
			int trimEnd = br.ReadInt32();
			int loopStart = br.ReadInt32();
			int loopEnd = br.ReadInt32();

			// Reconstruct a loaded AudioDocument from saved state
			var doc = new AudioDocument();
			var samples = WavDecoder.Decode(sourcePath, out int sampleRate);
			doc.ApplyLoad(samples, sampleRate, sourcePath);

			// Restore saved markers and flags — clamp defensively in case
			// the source WAV was replaced with a shorter one since last save
			doc.ApplySetMarker(MarkerKind.TrimStart, Math.Clamp(trimStart, 0, samples.Length - 1));
			doc.ApplySetMarker(MarkerKind.TrimEnd, Math.Clamp(trimEnd, 0, samples.Length - 1));
			doc.ApplySetMarker(MarkerKind.LoopStart, Math.Clamp(loopStart, 0, samples.Length - 1));
			doc.ApplySetMarker(MarkerKind.LoopEnd, Math.Clamp(loopEnd, 0, samples.Length - 1));

			if (flags.HasFlag(AudioFlags.Loop))
				doc.ApplyToggleLoop();

			return doc;
		}
	}
}
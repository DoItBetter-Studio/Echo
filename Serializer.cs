using System.IO;
using System.Text;

namespace Echo
{
	public static class Serializer
	{
		private const uint MAGIC = 0x44554145;  // "EAUD"
		private const ushort VERSION = 1;

		public static void Write(string name, AudioEditorDocument data)
		{
			string path = Path.Combine(EditorPaths.AssetsAudio, $"{name}.gbaud");

			using var fs = File.Create(path);
			using var bw = new BinaryWriter(fs);

			// Header
			bw.Write(MAGIC);
			bw.Write(VERSION);

			// Source path
			byte[] pathBytes = Encoding.UTF8.GetBytes(data.SourcePath);
			bw.Write((ushort)pathBytes.Length);
			bw.Write(pathBytes);

			// Flags
			bw.Write((byte) data.Flags);

			// Trim
			bw.Write(data.TrimStart);
			bw.Write(data.TrimEnd);

			// Loop
			bw.Write(data.LoopStart);
			bw.Write(data.LoopEnd);
		}

		public static AudioEditorDocument Read(string name)
		{
			string path = Path.Combine(EditorPaths.AssetsAudio, $"{name}.gbaud");

			using var fs = File.OpenRead(path);
			using var br = new BinaryReader(fs);

			uint magic = br.ReadUInt32();
			if (magic != MAGIC)
				throw new InvalidDataException("Invalid .gbaud file");

			ushort version = br.ReadUInt16();
			if (version != VERSION)
				throw new InvalidDataException($"Unsupported .gbaud version {version}");

			// Source path
			ushort pathLen = br.ReadUInt16();
			string sourcePath = System.Text.Encoding.UTF8.GetString(br.ReadBytes(pathLen));

			var data = new AudioEditorDocument
			{
				SourcePath = sourcePath,
				Flags = (AudioFlags) br.ReadByte(),
				TrimStart = br.ReadSingle(),
				TrimEnd = br.ReadSingle(),
				LoopStart = br.ReadSingle(),
				LoopEnd = br.ReadSingle(),
			};

			return data;
		}
	}
}

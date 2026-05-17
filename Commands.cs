using System;
using System.IO;

namespace Echo
{
	/// <summary>
	/// All mutations to AudioDocument go through here.
	/// The UI calls commands; commands call internal Apply* methods on the document.
	/// </summary>
	public static class Commands
	{
		// ---------------------------------------------------------------
		// Load
		// ---------------------------------------------------------------

		public static void LoadWav(AudioDocument doc, string path)
		{
			sbyte[] samples = WavDecoder.Decode(path, out int sampleRate);
			doc.ApplyLoad(samples, sampleRate, path);
		}

		// ---------------------------------------------------------------
		// Save editor project
		// ---------------------------------------------------------------

		public static void Save(AudioDocument doc, string name)
		{
			if (!doc.IsLoaded) return;
			Serializer.Write(name, doc);
		}

		// ---------------------------------------------------------------
		// Markers
		// ---------------------------------------------------------------

		public static void SetMarker(AudioDocument doc, MarkerKind kind, int sampleIndex)
		{
			if (!doc.IsLoaded) return;
			doc.ApplySetMarker(kind, sampleIndex);
		}

		public static void ToggleLoop(AudioDocument doc)
		{
			if (!doc.IsLoaded) return;
			doc.ApplyToggleLoop();
		}

		// ---------------------------------------------------------------
		// Export
		// ---------------------------------------------------------------

		public static void Export(AudioDocument doc, string outputPath)
		{
			if (!doc.IsLoaded) return;
			GbaudExporter.Write(doc, outputPath);
		}
	}
}
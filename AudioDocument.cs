using System;

namespace Echo
{
	public enum MarkerKind { TrimStart, TrimEnd, LoopStart, LoopEnd }

	public class AudioDocument
	{
		// Raw s8 PCM samples decoded from WAV, exactly as the runtime will consume them
		public sbyte[] Samples { get; private set; } = Array.Empty<sbyte>();
		public int SampleRate { get; private set; } = 44100;
		public bool IsLoaded => Samples.Length > 0;

		// All marker positions are sample indices into Samples[]
		public int TrimStart { get; private set; } = 0;
		public int TrimEnd { get; private set; } = 0;
		public int LoopStart { get; private set; } = 0;
		public int LoopEnd { get; private set; } = 0;
		public AudioFlags Flags { get; private set; } = AudioFlags.None;
		public bool LoopEnabled => Flags.HasFlag(AudioFlags.Loop);

		public string SourcePath { get; private set; } = string.Empty;

		public event EventHandler? StateChanged;

		// ---------------------------------------------------------------
		// Internal mutators — only Commands.cs calls these
		// ---------------------------------------------------------------

		internal void ApplyLoad(sbyte[] samples, int sampleRate, string path)
		{
			Samples = samples;
			SampleRate = sampleRate;
			SourcePath = path;
			TrimStart = 0;
			TrimEnd = samples.Length - 1;
			LoopStart = 0;
			LoopEnd = samples.Length - 1;
			Flags = AudioFlags.None;
			RaiseStateChanged();
		}

		internal void ApplySetMarker(MarkerKind kind, int sampleIndex)
		{
			sampleIndex = Math.Clamp(sampleIndex, 0, Samples.Length - 1);

			switch (kind)
			{
				case MarkerKind.TrimStart:
					TrimStart = Math.Min(sampleIndex, TrimEnd - 1);
					break;
				case MarkerKind.TrimEnd:
					TrimEnd = Math.Max(sampleIndex, TrimStart + 1);
					break;
				case MarkerKind.LoopStart:
					LoopStart = Math.Min(sampleIndex, LoopEnd - 1);
					break;
				case MarkerKind.LoopEnd:
					LoopEnd = Math.Max(sampleIndex, LoopStart + 1);
					break;
			}

			RaiseStateChanged();
		}

		internal void ApplyToggleLoop()
		{
			if (Flags.HasFlag(AudioFlags.Loop))
				Flags &= ~AudioFlags.Loop;
			else
				Flags |= AudioFlags.Loop;

			RaiseStateChanged();
		}

		internal void RaiseStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);
	}
}
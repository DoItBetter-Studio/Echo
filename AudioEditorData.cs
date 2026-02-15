using System;

namespace Echo
{
	[Flags]
	public enum AudioFlags : byte
	{
		None		= 0,
		ForceMono	= 1 << 0,
		Loop		= 1 << 1,
	}

	public class AudioEditorDocument
	{
		public string SourcePath;

		public AudioFlags Flags;

		public float TrimStart;
		public float TrimEnd;

		public float LoopStart;
		public float LoopEnd;
	}
}

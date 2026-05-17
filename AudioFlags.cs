using System;

namespace Echo
{
	[Flags]
	public enum AudioFlags : byte
	{
		None = 0,
		Loop = 1 << 0,
	}
}
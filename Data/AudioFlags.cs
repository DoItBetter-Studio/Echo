using System;

namespace Echo.Data
{
	[Flags]
	public enum AudioFlags : byte
	{
		None = 0,
		Loop = 1 << 0,
	}
}
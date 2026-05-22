using System;
using System.IO;

namespace Echo.Editor
{
	public static class EditorPaths
	{
		private static string AssetsRoot => Path.Combine(AppContext.BaseDirectory, "../..", "assets");
		private static string DataRoot => Path.Combine(AppContext.BaseDirectory, "../..", "data");

		public static string AssetsAudio => Path.Combine(AssetsRoot, "audio");
		public static string DataAudio => Path.Combine(DataRoot, "audio");
	}
}
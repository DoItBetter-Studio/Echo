using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Echo
{
	public static class VersionChecker
	{
		const string LOCAL_VERSION = "0.9.0";
		const string VERSION_URL = "https://raw.githubusercontent.com/DoItBetter-Studio/Glyphborn-Tools/main/Echo/version.txt";
		const string UPDATE_ZIP = "update.zip";

		static async Task<string> GetRemoteVersionAsync()
		{
			using var client = new HttpClient();
			var text = await client.GetStringAsync(VERSION_URL);
			return text.Trim();
		}

		public static async Task CheckForUpdatesAsync()
		{
			var remote = await GetRemoteVersionAsync();

			Version current = new Version(LOCAL_VERSION);
			Version version = new Version(remote);

			if (version > current)
			{
				await DownloadUpdateAsync(remote);
				LaunchUpdaterAndExit();
			}
		}

		static async Task DownloadUpdateAsync(string version)
		{
			string url = $"https://github.com/DoItBetter-Studio/Glyphborn-Tools/releases/download/v{version}/Echo.zip";

			using var client = new HttpClient();
			var data = await client.GetByteArrayAsync(url);
			await File.WriteAllBytesAsync(UPDATE_ZIP, data);
		}

		static void LaunchUpdaterAndExit()
		{
			string exePath	= Environment.ProcessPath!;
			string exeDir	= Path.GetDirectoryName(exePath)!;
			string zipPath	= Path.Combine(exeDir, UPDATE_ZIP);

			var psi = new ProcessStartInfo
			{
				FileName = Path.Combine(exeDir, "Updater.exe"),
				Arguments = $"\"{zipPath}\" \"{exePath}\"",
				UseShellExecute = false
			};

			Process.Start(psi);
			Environment.Exit(0);
		}
	}
}

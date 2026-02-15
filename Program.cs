using System;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Echo
{
	internal static class Program
	{
		/// <summary>
		///  The main entry point for the application.
		/// </summary>
		[STAThread]
		static async Task Main()
		{
			//await VersionChecker.CheckForUpdatesAsync();
			
			// To customize application configuration such as set high DPI settings or default font,
			// see https://aka.ms/applicationconfiguration.
			ApplicationConfiguration.Initialize();
			Application.Run(new EchoForm());
		}
	}
}
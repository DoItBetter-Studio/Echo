using System.Drawing;
using System.Windows.Forms;
using Echo.Controls;

namespace Echo
{
	public partial class MainForm : Form
	{
		private SoundListView _soundListView = null!;

		public MainForm()
		{
			Text = "Echo — Glyphborn Audio Editor";
			Size = new Size(1200, 600);
			MinimumSize = new Size(800, 400);
			BackColor = Color.FromArgb(30, 30, 30);
			Font = new Font("Consolas", 9f);

			_soundListView = new SoundListView
			{
				Dock = DockStyle.Fill,
			};

			Controls.Add(_soundListView);
			_soundListView.BringToFront();
		}
	}
}
using System;
using System.Drawing;
using System.Windows.Forms;

using Echo.Controls;

namespace Echo
{
	public partial class EchoForm : Form
	{
		private SoundListView SoundListView;

		public EchoForm()
		{
			InitializeComponent();

			BackColor = Color.FromArgb(30, 30, 30);

			EchoForm_Load();
		}

		private void EchoForm_Load()
		{
			SoundListView = new SoundListView
			{
				Dock = DockStyle.Fill,
			};

			Controls.Add(SoundListView);
			SoundListView.BringToFront();
		}

		protected override void OnResize(EventArgs e)
		{
			base.OnResize(e);

			if (SoundListView != null )
				SoundListView.Invalidate();
		}
	}
}

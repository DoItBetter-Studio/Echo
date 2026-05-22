using Echo.Data;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Echo.Controls
{
	/// <summary>
	/// Pure stateless renderer. Takes an AudioDocument and dimensions, returns a Bitmap.
	/// Owns no state whatsoever — call Render() anytime StateChanged fires.
	/// </summary>
	public static class WaveformRenderer
	{
		// ---------------------------------------------------------------
		// Colours — matching the Glyphborn Echo dark theme
		// ---------------------------------------------------------------
		private static readonly Color BgColor = Color.FromArgb(255, 22, 24, 28);
		private static readonly Color TrackBgColor = Color.FromArgb(255, 32, 38, 44);
		private static readonly Color WaveColor = Color.FromArgb(255, 80, 200, 140);
		private static readonly Color WaveFillColor = Color.FromArgb(60, 80, 200, 140);
		private static readonly Color TrimShadeColor = Color.FromArgb(100, 0, 0, 0);
		private static readonly Color TrimMarker = Color.FromArgb(255, 255, 180, 60);  // orange
		private static readonly Color LoopMarker = Color.FromArgb(255, 60, 220, 220); // cyan
		private static readonly Color GridColor = Color.FromArgb(30, 255, 255, 255);
		private static readonly Color TextColor = Color.FromArgb(180, 255, 255, 255);

		private const int MarkerWidth = 2;
		private const int MarkerCapH = 8;
		private const int MarkerCapW = 10;

		// ---------------------------------------------------------------
		// Pulbic API
		// ---------------------------------------------------------------

		public static Bitmap Render(AudioDocument doc, int width, int height)
		{
			var bmp = new Bitmap(width, height);
			using var g = Graphics.FromImage(bmp);
			g.SmoothingMode = SmoothingMode.None;
			g.PixelOffsetMode = PixelOffsetMode.Half; // Sharp 1px lines

			DrawBackground(g, width, height);

			if (!doc.IsLoaded)
			{
				DrawEmptyHint(g, width, height);
				return bmp;
			}

			DrawGrid(g, width, height, doc);
			DrawWaveform(g, width, height, doc);
			DrawTrimShade(g, width, height, doc);
			DrawMarkers(g, width, height, doc);

			return bmp;
		}

		/// <summary>
		/// Given a pixel X in the waveform panel, returns the sample index it maps to.
		/// </summary>
		public static int XToSample(AudioDocument doc, int x, int width)
		{
			if (!doc.IsLoaded || width <= 0) return 0;
			double ratio = Math.Clamp((double)x / width, 0.0, 1.0);
			return (int)(ratio * (doc.Samples.Length - 1));
		}

		/// <summary>
		/// Given a sample index, returns its pixel X in a panel of the given width.
		/// </summary>
		public static int SampleToX(AudioDocument doc, int sampleIndex, int width)
		{
			if (!doc.IsLoaded || doc.Samples.Length <= 1) return 0;
			return (int)((double)sampleIndex / (doc.Samples.Length - 1) * width);
		}

		/// <summary>
		/// Returns which marker (if any) is within grab distance of pixel x.
		/// Returns null if none.
		/// </summary>
		public static MarkerKind? HitTestMarker(AudioDocument doc, int x, int width, int grabRadius = 6)
		{
			if (!doc.IsLoaded) return null;

			int xTS = SampleToX(doc, doc.TrimStart, width);
			int xTE = SampleToX(doc, doc.TrimEnd, width);
			int xLS = SampleToX(doc, doc.LoopStart, width);
			int xLE = SampleToX(doc, doc.LoopEnd, width);

			// Priority order: trim markers checked first, loop second
			if (Math.Abs(x - xTS) <= grabRadius) return MarkerKind.TrimStart;
			if (Math.Abs(x - xTE) <= grabRadius) return MarkerKind.TrimEnd;
			if (Math.Abs(x - xLS) <= grabRadius) return MarkerKind.LoopStart;
			if (Math.Abs(x - xLE) <= grabRadius) return MarkerKind.LoopEnd;

			return null;
		}

		// ---------------------------------------------------------------
		// Private drawing helpers
		// ---------------------------------------------------------------

		private static void DrawBackground(Graphics g, int width, int height)
		{
			using var bgBrush = new SolidBrush(BgColor);
			g.FillRectangle(bgBrush, 0, 0, width, height);
		}

		private static void DrawEmptyHint(Graphics g, int width, int height)
		{
			using var font = new Font("Consolas", 11f, FontStyle.Regular);
			using var brush = new SolidBrush(Color.FromArgb(80, 255, 255, 255));
			string msg = "Drag a .wav file here or use File > Open";
			var size = g.MeasureString(msg, font);
			g.DrawString(msg, font, brush,
				(width - size.Width) / 2f,
				(height - size.Height) / 2f);
		}

		private static void DrawGrid(Graphics g, int width, int height, AudioDocument doc)
		{
			using var pen = new Pen(GridColor, 1f);

			// Vertical lines every ~10% of the sample length
			int divisions = 10;
			for (int i = 1; i < divisions; i++)
			{
				int x = width * i / divisions;
				g.DrawLine(pen, x, 0, x, height);
			}

			// Centre line
			pen.Color = Color.FromArgb(50, 255, 255, 255);
			g.DrawLine(pen, 0, height / 2, width, height / 2);
		}

		private static void DrawWaveform(Graphics g, int width, int height, AudioDocument doc)
		{
			// Track background
			using var trackBrush = new SolidBrush(TrackBgColor);
			g.FillRectangle(trackBrush, 0, 0, width, height);

			if (doc.Samples.Length == 0 || width == 0) return;

			int halfH = height / 2;

			// Build fill polygon - top peaks going left->right, then bottom peaks going right->left
			var topPoints = new List<Point>(width);
			var bottomPoints = new List<Point>(width);

			for (int px = 0; px < width; px++)
			{
				int sampleStart = (int)((long)px * doc.Samples.Length / width);
				int sampleEnd = (int)((long)(px + 1) * doc.Samples.Length / width);
				if (sampleEnd >= doc.Samples.Length) sampleEnd = doc.Samples.Length - 1;
				if (sampleEnd < sampleStart) sampleEnd = sampleStart;

				sbyte min = 127, max = -128;
				for (int s = sampleStart; s <= sampleEnd; s++)
				{
					if (doc.Samples[s] < min) min = doc.Samples[s];
					if (doc.Samples[s] > max) max = doc.Samples[s];
				}

				int yTop = halfH - (int)(max / 128.0 * halfH);
				int yBottom = halfH - (int)(min / 128.0 * halfH);

				topPoints.Add(new Point(px, yTop));
				bottomPoints.Add(new Point(px, yBottom));
			}

			// Fill between top and bottom
			var poly = new List<Point>(topPoints.Count + bottomPoints.Count);
			poly.AddRange(topPoints);
			bottomPoints.Reverse();
			poly.AddRange(bottomPoints);

			if (poly.Count >= 3)
			{
				using var fillBrush = new SolidBrush(WaveFillColor);
				g.FillPolygon(fillBrush, poly.ToArray());
			}

			// Draw top and bottom outlines
			using var wavePen = new Pen(WaveColor, 1f);
			if (topPoints.Count >= 2) g.DrawLines(wavePen, topPoints.ToArray());
			if (bottomPoints.Count >= 2)
			{
				bottomPoints.Reverse();
				g.DrawLines(wavePen, bottomPoints.ToArray());
			}
		}

		private static void DrawTrimShade(Graphics g, int width, int height, AudioDocument doc)
		{
			using var shade = new SolidBrush(TrimShadeColor);

			int xTS = SampleToX(doc, doc.TrimStart, width);
			int xTE = SampleToX(doc, doc.TrimEnd, width);

			// Left dead zone
			if (xTS > 0)
				g.FillRectangle(shade, 0, 0, xTS, height);

			// Right dead zone
			if (xTE < width)
				g.FillRectangle(shade, xTE, 0, width - xTE, height);
		}

		private static void DrawMarkers(Graphics g, int width, int height, AudioDocument doc)
		{
			DrawMarker(g, height, SampleToX(doc, doc.TrimStart, width), TrimMarker, capUp: true);
			DrawMarker(g, height, SampleToX(doc, doc.TrimEnd, width), TrimMarker, capUp: true);

			if (doc.LoopEnabled)
			{
				DrawMarker(g, height, SampleToX(doc, doc.LoopStart, width), LoopMarker, capUp: false);
				DrawMarker(g, height, SampleToX(doc, doc.LoopEnd, width), LoopMarker, capUp: false);
			}
		}

		private static void DrawMarker(Graphics g, int height, int x, Color color, bool capUp)
		{
			using var pen = new Pen(color, MarkerWidth);
			using var brush = new SolidBrush(color);

			g.DrawLine(pen, x, 0, x, height);

			// Diamond cap
			int capY = capUp ? 0 : height;
			int dir = capUp ? 1 : -1;

			Point[] cap =
			{
				new Point(x,                    capY),
				new Point(x + MarkerCapW / 2,   capY + dir * MarkerCapH),
				new Point(x - MarkerCapW / 2,   capY + dir * MarkerCapH),
			};

			g.FillPolygon(brush, cap);
		}
	}
}

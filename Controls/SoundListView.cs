using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace Echo.Controls
{
	public class SoundListView : Control
	{
		// ---------------------------------------------------------------
		// Layout constants (Maintained for column/scrolling offsets)
		// ---------------------------------------------------------------
		private const int ColName = 36;
		private const int ColDuration = 336;
		private const int ColChannels = 536;
		private const int ColSampleRate = 636;
		private const int RowHeight = 32;
		private const int RowGap = 2;
		private const int WaveHeight = 64;
		private const int WaveOffsetY = 52;
		private const int WaveMarginX = 10;
		private const int TrimBandH = 10;
		private const int LoopBandH = 10;
		private const int GrabRadius = 6;
		private const int HeaderHeight = 32;

		private const int MarkerCapW = 10;
		private const int MarkerCapH = 8;

		// ---------------------------------------------------------------
		// Colors — Extracted from Glyphborn Echo Dark Theme (WaveformRenderer)
		// ---------------------------------------------------------------
		private readonly Brush _bgBrush = new SolidBrush(Color.FromArgb(255, 22, 24, 28));
		private readonly Brush _rowBrush1 = new SolidBrush(Color.FromArgb(255, 32, 38, 44));
		private readonly Brush _rowBrush2 = new SolidBrush(Color.FromArgb(255, 26, 30, 35));
		private readonly Color _colorTrim = Color.FromArgb(255, 255, 180, 60);
		private readonly Color _colorLoop = Color.FromArgb(255, 60, 220, 220);
		private readonly Brush _textMuted = new SolidBrush(Color.FromArgb(160, 160, 160));

		// ---------------------------------------------------------------
		// Items and selection
		// ---------------------------------------------------------------
		private readonly List<SoundItem> _items = new();
		private int _selectedIndex = -1;
		private int _scrollOffset = 0;

		// ---------------------------------------------------------------
		// Drag state
		// ---------------------------------------------------------------
		private DragMode _dragMode = DragMode.None;
		private SoundItem? _dragItem = null;
		private Rectangle _dragWaveRect;

		// ---------------------------------------------------------------
		// Playback
		// ---------------------------------------------------------------
		private readonly WinMMAudioPlayer _player = new WinMMAudioPlayer();
		private SoundItem? _playingItem = null;

		// ---------------------------------------------------------------
		// Batch export flash
		// ---------------------------------------------------------------
		private bool _batchFlash = false;
		private readonly System.Windows.Forms.Timer _flashTimer = new System.Windows.Forms.Timer { Interval = 1000 };

		// ---------------------------------------------------------------
		// Constructor
		// ---------------------------------------------------------------
		public SoundListView()
		{
			DoubleBuffered = true;
			Font = new Font("Consolas", 9f);
			LoadAudio();

			_player.PlaybackStopped += (_, __) =>
			{
				if (_playingItem != null)
					_playingItem.IsPlaying = false;
				_playingItem = null;
				Invalidate();
			};

			_flashTimer.Tick += (_, __) =>
			{
				_batchFlash = false;
				_flashTimer.Stop();
				Invalidate();
			};
		}

		// ---------------------------------------------------------------
		// Paint
		// ---------------------------------------------------------------
		protected override void OnPaint(PaintEventArgs e)
		{
			var g = e.Graphics;
			g.SmoothingMode = SmoothingMode.AntiAlias;

			// Clear background with editor standard base tone
			g.FillRectangle(_bgBrush, ClientRectangle);

			// Column headers
			g.DrawString("Name", Font, _textMuted, ColName, 8);
			g.DrawString("Duration", Font, _textMuted, ColDuration, 8);
			g.DrawString("Channels", Font, _textMuted, ColChannels, 8);
			g.DrawString("Sample Rate", Font, _textMuted, ColSampleRate, 8);

			// Batch export button — top-right of header row
			var batchRect = GetBatchExportRect();
			using (var batchBrush = new SolidBrush(_batchFlash ? Color.FromArgb(60, 180, 80) : Color.FromArgb(50, 50, 55)))
				g.FillRectangle(batchBrush, batchRect);
			g.DrawString(_batchFlash ? "Exported!" : "Export All", Font,
				_batchFlash ? Brushes.White : Brushes.LightGray,
				batchRect.X + 6, batchRect.Y + 4);

			int y = HeaderHeight - _scrollOffset;

			for (int i = 0; i < _items.Count; i++)
			{
				var item = _items[i];
				bool sel = i == _selectedIndex;
				int height = sel ? item.ExpandedHeight : item.CollapsedHeight;

				DrawItem(g, item, new Rectangle(0, y, Width, height), sel, i);

				y += height + RowGap;
			}
		}

		private void DrawItem(Graphics g, SoundItem item, Rectangle rect, bool isSelected, int index)
		{
			// Row background matching single view editor container bases
			g.FillRectangle((index % 2 == 0) ? _rowBrush1 : _rowBrush2, rect);

			// Play/stop button - flat minimalist styling matching theme
			g.FillEllipse(Brushes.WhiteSmoke, rect.X + 4, rect.Y + 4, 24, 24);
			if (item.IsPlaying)
				DrawStopIcon(g, new Rectangle(rect.X + 4, rect.Y + 4, 24, 24));
			else
				DrawPlayIcon(g, new Rectangle(rect.X + 4, rect.Y + 4, 24, 24));

			// Metadata Columns text colors
			var doc = item.Document;
			double duration = (double)doc.Samples.Length / doc.SampleRate;
			int minutes = (int)(duration / 60);
			double seconds = duration % 60;

			g.DrawString(item.Name, Font, Brushes.White, rect.X + ColName, rect.Y + 8);
			g.DrawString($"{minutes:D2}:{seconds:00.000}", Font, Brushes.White, rect.X + ColDuration, rect.Y + 8);
			g.DrawString("1", Font, Brushes.White, rect.X + ColChannels, rect.Y + 8);
			g.DrawString(doc.SampleRate.ToString(), Font, Brushes.White, rect.X + ColSampleRate, rect.Y + 8);

			if (!isSelected) return;

			// ---------------------------------------------------------------
			// Expanded waveform area via WaveformRenderer
			// ---------------------------------------------------------------
			var waveRect = GetWaveRect(rect);
			var trimBand = new Rectangle(waveRect.Left, waveRect.Top - TrimBandH, waveRect.Width, TrimBandH);
			var loopBand = new Rectangle(waveRect.Left, waveRect.Bottom, waveRect.Width, LoopBandH);

			// Section title text formatting
			g.DrawString("Waveform Visualizer", Font, _textMuted, waveRect.X, waveRect.Y - 20);

			// Check and regenerate cache
			if (item.CachedWaveform == null || item.CachedWaveform.Width != waveRect.Width || item.CachedWaveform.Height != waveRect.Height)
			{
				item.CachedWaveform?.Dispose();
				item.CachedWaveform = WaveformRenderer.Render(item.Document, waveRect.Width, waveRect.Height);
			}

			// Draw full-bleed bitmap layer directly seamlessly matching single editor panel
			g.DrawImage(item.CachedWaveform, waveRect.X, waveRect.Y);

			// Draw stylized vector Trim marker caps over control handle boundaries
			int trimStartX = SampleToX(doc, doc.TrimStart, waveRect);
			int trimEndX = SampleToX(doc, doc.TrimEnd, waveRect);

			DrawCapMarker(g, trimStartX, trimBand.Top, _colorTrim, capUp: true);
			DrawCapMarker(g, trimEndX, trimBand.Top, _colorTrim, capUp: true);

			// Draw stylized vector Loop marker caps ONLY if looping is active
			bool looping = doc.LoopEnabled;
			if (looping)
			{
				int loopStartX = SampleToX(doc, doc.LoopStart, waveRect);
				int loopEndX = SampleToX(doc, doc.LoopEnd, waveRect);

				DrawCapMarker(g, loopStartX, loopBand.Top, _colorLoop, capUp: false);
				DrawCapMarker(g, loopEndX, loopBand.Top, _colorLoop, capUp: false);
			}

			// ---------------------------------------------------------------
			// Unbound Persistent Active Status Footer Layout (Split Rendering)
			// ---------------------------------------------------------------
			double trimDuration = (double)(doc.TrimEnd - doc.TrimStart) / doc.SampleRate;

			Brush labelBrush = Brushes.MediumSpringGreen;
			Brush valueBrush = Brushes.WhiteSmoke;
			Brush pipeBrush = Brushes.DimGray;
			Brush loopStateBrush = looping ? Brushes.MediumSpringGreen : Brushes.Firebrick;

			float currentX = rect.X + WaveMarginX;
			float footerY = waveRect.Bottom + LoopBandH + 2;

			void DrawTextSegment(string text, Brush brush)
			{
				g.DrawString(text, Font, brush, currentX, footerY);
				currentX += g.MeasureString(text, Font).Width;
			}

			// 1. Loop prefix and conditional state coloring
			DrawTextSegment("Loop: ", labelBrush);
			DrawTextSegment(looping ? "Active" : "Disabled", loopStateBrush);

			// 2. Trim Info Block
			DrawTextSegment("    |    ", pipeBrush);
			DrawTextSegment("Trim: ", labelBrush);
			DrawTextSegment($"{doc.TrimStart}–{doc.TrimEnd}", valueBrush);

			// 3. Loop Points Block
			DrawTextSegment("    |    ", pipeBrush);
			DrawTextSegment("Loop pts: ", labelBrush);
			DrawTextSegment($"{doc.LoopStart}–{doc.LoopEnd}", valueBrush);

			// 4. Duration Block
			DrawTextSegment("    |    ", pipeBrush);
			DrawTextSegment("Duration: ", labelBrush);
			DrawTextSegment($"{trimDuration:F3}s", valueBrush);

			// Per-item export button — right side of footer
			var exportRect = GetExportButtonRect(rect, waveRect);
			using (var exportBrush = new SolidBrush(Color.FromArgb(50, 50, 55)))
				g.FillRectangle(exportBrush, exportRect);
			g.DrawString("Export", Font, Brushes.LightGray, exportRect.X + 6, exportRect.Y + 2);
		}

		private static void DrawCapMarker(Graphics g, int x, int y, Color color, bool capUp)
		{
			using var brush = new SolidBrush(color);
			int dir = capUp ? 1 : -1;
			int baseOffset = capUp ? 0 : LoopBandH;

			Point[] points = {
				new Point(x, y + baseOffset),
				new Point(x + MarkerCapW / 2, y + baseOffset + (dir * MarkerCapH)),
				new Point(x - MarkerCapW / 2, y + baseOffset + (dir * MarkerCapH))
			};
			g.FillPolygon(brush, points);
		}

		// ---------------------------------------------------------------
		// Mouse
		// ---------------------------------------------------------------
		protected override void OnMouseDown(MouseEventArgs e)
		{
			// Batch export button in header
			if (GetBatchExportRect().Contains(e.Location))
			{
				BatchExport();
				return;
			}

			int y = HeaderHeight - _scrollOffset;

			for (int i = 0; i < _items.Count; i++)
			{
				var item = _items[i];
				bool sel = i == _selectedIndex;
				int height = sel ? item.ExpandedHeight : item.CollapsedHeight;
				var rect = new Rectangle(0, y, Width, height);

				var playBtn = new Rectangle(rect.X + 4, rect.Y + 4, 24, 24);
				if (playBtn.Contains(e.Location))
				{
					TogglePlayback(item);
					Invalidate();
					return;
				}

				var header = new Rectangle(rect.X + ColName, rect.Y, Width - ColName, RowHeight);
				if (header.Contains(e.Location))
				{
					_selectedIndex = (i == _selectedIndex) ? -1 : i;
					AutoScrollToItem(i);
					Invalidate();
					return;
				}

				if (sel)
				{
					var waveRect = GetWaveRect(rect);
					var trimBand = new Rectangle(waveRect.Left, waveRect.Top - TrimBandH, waveRect.Width, TrimBandH);
					var loopBand = new Rectangle(waveRect.Left, waveRect.Bottom, waveRect.Width, LoopBandH);

					// Loop toggle
					var loopToggle = new Rectangle(rect.X + WaveMarginX, waveRect.Bottom + LoopBandH + 2, 100, 16);
					if (loopToggle.Contains(e.Location))
					{
						Commands.ToggleLoop(item.Document);
						Commands.Save(item.Document, item.Name);
						item.ClearCache();
						Invalidate();
						return;
					}

					// Per-item export button
					var exportBtn = GetExportButtonRect(rect, waveRect);
					if (exportBtn.Contains(e.Location))
					{
						ExportSingle(item);
						return;
					}

					int trimStartX = SampleToX(item.Document, item.Document.TrimStart, waveRect);
					int trimEndX = SampleToX(item.Document, item.Document.TrimEnd, waveRect);

					if (Math.Abs(e.X - trimStartX) <= GrabRadius && trimBand.Top <= e.Y && e.Y <= trimBand.Bottom)
					{
						_dragMode = DragMode.TrimStart; _dragItem = item; _dragWaveRect = waveRect; return;
					}
					if (Math.Abs(e.X - trimEndX) <= GrabRadius && trimBand.Top <= e.Y && e.Y <= trimBand.Bottom)
					{
						_dragMode = DragMode.TrimEnd; _dragItem = item; _dragWaveRect = waveRect; return;
					}

					if (item.Document.LoopEnabled)
					{
						int loopStartX = SampleToX(item.Document, item.Document.LoopStart, waveRect);
						int loopEndX = SampleToX(item.Document, item.Document.LoopEnd, waveRect);

						if (Math.Abs(e.X - loopStartX) <= GrabRadius && loopBand.Top <= e.Y && e.Y <= loopBand.Bottom)
						{
							_dragMode = DragMode.LoopStart; _dragItem = item; _dragWaveRect = waveRect; return;
						}
						if (Math.Abs(e.X - loopEndX) <= GrabRadius && loopBand.Top <= e.Y && e.Y <= loopBand.Bottom)
						{
							_dragMode = DragMode.LoopEnd; _dragItem = item; _dragWaveRect = waveRect; return;
						}
					}
				}

				y += height + RowGap;
			}
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			if (_dragMode == DragMode.None || _dragItem == null) return;

			var doc = _dragItem.Document;
			int sample = XToSample(doc, e.X, _dragWaveRect);

			switch (_dragMode)
			{
				case DragMode.TrimStart:
					Commands.SetMarker(doc, MarkerKind.TrimStart, sample);
					break;
				case DragMode.TrimEnd:
					Commands.SetMarker(doc, MarkerKind.TrimEnd, sample);
					break;
				case DragMode.LoopStart:
					Commands.SetMarker(doc, MarkerKind.LoopStart, sample);
					break;
				case DragMode.LoopEnd:
					Commands.SetMarker(doc, MarkerKind.LoopEnd, sample);
					break;
			}

			_dragItem.ClearCache();
			Invalidate();
		}

		protected override void OnMouseUp(MouseEventArgs e)
		{
			if (_dragItem != null)
				Commands.Save(_dragItem.Document, _dragItem.Name);

			_dragMode = DragMode.None;
			_dragItem = null;
		}

		protected override void OnMouseWheel(MouseEventArgs e)
		{
			_scrollOffset -= e.Delta / 120 * 20;
			_scrollOffset = Math.Clamp(_scrollOffset, 0, Math.Max(0, TotalHeight() - ClientSize.Height));
			Invalidate();
		}

		// ---------------------------------------------------------------
		// Playback
		// ---------------------------------------------------------------
		private void TogglePlayback(SoundItem item)
		{
			if (item.IsPlaying)
			{
				_player.Stop();
				item.IsPlaying = false;
				_playingItem = null;
			}
			else
			{
				if (_playingItem != null)
				{
					_player.Stop();
					_playingItem.IsPlaying = false;
				}

				item.IsPlaying = true;
				_playingItem = item;
				_player.Play(item.Document);
			}
		}

		// ---------------------------------------------------------------
		// Export
		// ---------------------------------------------------------------
		private void ExportSingle(SoundItem item)
		{
			Directory.CreateDirectory(EditorPaths.DataAudio);
			string outPath = Path.Combine(EditorPaths.DataAudio, $"{item.Name}.gbaud");
			Commands.Export(item.Document, outPath);
		}

		private void BatchExport()
		{
			Directory.CreateDirectory(EditorPaths.DataAudio);
			foreach (var item in _items)
			{
				string outPath = Path.Combine(EditorPaths.DataAudio, $"{item.Name}.gbaud");
				Commands.Export(item.Document, outPath);
			}

			_batchFlash = true;
			_flashTimer.Stop();
			_flashTimer.Start();
			Invalidate();
		}

		// ---------------------------------------------------------------
		// Rect helpers
		// ---------------------------------------------------------------
		private Rectangle GetBatchExportRect() =>
			new Rectangle(Width - 90, 4, 82, 22);

		private static Rectangle GetExportButtonRect(Rectangle itemRect, Rectangle waveRect) =>
			new Rectangle(itemRect.Right - 70, waveRect.Bottom + LoopBandH + 2, 62, 16);

		// ---------------------------------------------------------------
		// Load audio from assets/audio/
		// ---------------------------------------------------------------
		private void LoadAudio()
		{
			if (!Directory.Exists(EditorPaths.AssetsAudio))
				Directory.CreateDirectory(EditorPaths.AssetsAudio);

			var wavExtensions = new[] { ".wav" };

			foreach (var f in Directory.EnumerateFiles(EditorPaths.AssetsAudio))
			{
				string ext = Path.GetExtension(f).ToLowerInvariant();
				if (Array.IndexOf(wavExtensions, ext) < 0) continue;

				try
				{
					string baseName = Path.GetFileNameWithoutExtension(f);
					AudioDocument doc;
					try
					{
						doc = Serializer.Read(baseName);
					}
					catch
					{
						doc = new AudioDocument();
						Commands.LoadWav(doc, f);
					}

					_items.Add(new SoundItem
					{
						Name = baseName,
						Document = doc
					});
				}
				catch (Exception ex)
				{
					Console.WriteLine($"Echo: failed to load {f}: {ex.Message}");
				}
			}
		}

		// ---------------------------------------------------------------
		// Coordinate helpers
		// ---------------------------------------------------------------
		private static Rectangle GetWaveRect(Rectangle itemRect) =>
			new Rectangle(itemRect.X + WaveMarginX, itemRect.Y + WaveOffsetY, itemRect.Width - WaveMarginX * 2, WaveHeight);

		private static int SampleToX(AudioDocument doc, int sample, Rectangle waveRect)
		{
			if (doc.Samples.Length <= 1) return waveRect.Left;
			double ratio = (double)sample / (doc.Samples.Length - 1);
			return waveRect.Left + (int)(ratio * waveRect.Width);
		}

		private static int XToSample(AudioDocument doc, int x, Rectangle waveRect)
		{
			if (waveRect.Width <= 0) return 0;
			double t = (double)(x - waveRect.Left) / waveRect.Width;
			t = Math.Clamp(t, 0.0, 1.0);
			return (int)(t * (doc.Samples.Length - 1));
		}

		// ---------------------------------------------------------------
		// Scroll helpers
		// ---------------------------------------------------------------
		private int TotalHeight()
		{
			int total = HeaderHeight;
			for (int i = 0; i < _items.Count; i++)
				total += ((i == _selectedIndex) ? _items[i].ExpandedHeight : _items[i].CollapsedHeight) + RowGap;
			return total;
		}

		private void AutoScrollToItem(int index)
		{
			int y = HeaderHeight;
			for (int i = 0; i < index; i++)
				y += ((i == _selectedIndex) ? _items[i].ExpandedHeight : _items[i].CollapsedHeight) + RowGap;

			int expandH = _items[index].ExpandedHeight;
			int viewH = ClientSize.Height;

			if (y + expandH > _scrollOffset + viewH)
				_scrollOffset = y + expandH - viewH;
			if (y < _scrollOffset)
				_scrollOffset = y;
			_scrollOffset = Math.Max(0, _scrollOffset);
		}

		// ---------------------------------------------------------------
		// Icons
		// ---------------------------------------------------------------
		private static void DrawPlayIcon(Graphics g, Rectangle bounds)
		{
			int cx = bounds.X + bounds.Width / 2;
			int cy = bounds.Y + bounds.Height / 2;
			int sz = bounds.Width / 3;

			g.FillPolygon(Brushes.DarkSlateGray, new Point[]
			{
				new Point(cx - sz / 2, cy - sz),
				new Point(cx - sz / 2, cy + sz),
				new Point(cx + sz,     cy),
			});
		}

		private static void DrawStopIcon(Graphics g, Rectangle bounds)
		{
			int sz = bounds.Width / 3;
			int x = bounds.X + (bounds.Width - sz) / 2;
			int y = bounds.Y + (bounds.Height - sz) / 2;
			g.FillRectangle(Brushes.DarkSlateGray, x, y, sz, sz);
		}

		// ---------------------------------------------------------------
		// Cleanup
		// ---------------------------------------------------------------
		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				_player.Dispose();
				_flashTimer.Dispose();
				_bgBrush.Dispose();
				_rowBrush1.Dispose();
				_rowBrush2.Dispose();
				_textMuted.Dispose();

				foreach (var item in _items)
					item.CachedWaveform?.Dispose();
			}
			base.Dispose(disposing);
		}
	}

	// ---------------------------------------------------------------
	// Supporting types
	// ---------------------------------------------------------------
	internal class SoundItem
	{
		public int CollapsedHeight => 32;
		public int ExpandedHeight => 160;

		public string Name { get; set; } = string.Empty;
		public AudioDocument Document { get; set; } = null!;
		public Bitmap? CachedWaveform { get; set; } = null;
		public bool IsPlaying { get; set; }

		public void ClearCache()
		{
			CachedWaveform?.Dispose();
			CachedWaveform = null;
		}
	}

	internal enum DragMode { None, TrimStart, TrimEnd, LoopStart, LoopEnd }
}
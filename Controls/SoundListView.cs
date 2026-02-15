using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Echo.Controls
{
	public class SoundListView : Control
	{
		const int ColName = 36;
		const int ColDuration = 336;
		const int ColChannels = 536;
		const int ColSampleRate = 636;
		const int TrimHandleHeight = 10;
		const int LoopHandleHeight = 10;

		private List<SoundItem> _items = new();
		private int _scrollOffset;
		private int _selectedIndex = -1;
		private Brush _brush1 = new SolidBrush(Color.FromArgb(60, 60, 65));
		private Brush _brush2 = new SolidBrush(Color.FromArgb(45, 45, 48));

		private WaveOutEvent output = new WaveOutEvent();

		private DragMode dragMode = DragMode.None;
		private SoundItem dragItem;

		private Rectangle waveformRect;
		private Rectangle trimStartHandle;
		private Rectangle trimEndHandle;
		private Rectangle loopStartHandle;
		private Rectangle loopEndHandle;
		private Rectangle loopToggleRect;

		public SoundListView()
		{
			LoadAudio();

			DoubleBuffered = true;
		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			int y = -_scrollOffset + 32;

			for (int i = 0; i < _items.Count; i++)
			{
				var item = _items[i];
				int height = (i == _selectedIndex) ? item.ExpandedHeight : item.Collapsed;

				// Header click (expand/collapse)
				if (e.Y >= y && e.Y <= y + 32 && e.X >= 32 && e.X <= Width)
				{
					_selectedIndex = (i == _selectedIndex) ? -1 : i;
					AutoScrollToItem(i);
					Invalidate();
					break;
				}
				// Play button click
				else if (e.Y >= y && e.Y <= y + 32 && e.X >= 4 && e.X <= 28)
				{
					item.isPlaying = !item.isPlaying;
					if (item.isPlaying)
						PreviewSound(item);
					else
						StopAllSounds();
					Invalidate();
					break;
				}

				// Only handle waveform interactions for the selected item
				if (i == _selectedIndex)
				{
					// Recalculate rectangles for THIS item
					var waveRect = new Rectangle(10, y + 52, Width - 20, 64);
					var trimBand = new Rectangle(waveRect.Left, waveRect.Top - TrimHandleHeight, waveRect.Width, TrimHandleHeight);
					var loopBand = new Rectangle(waveRect.Left, waveRect.Bottom, waveRect.Width, LoopHandleHeight);

					float ts = item.EditorDocument.TrimStart;
					float te = item.EditorDocument.TrimEnd;

					int trimStartX = waveRect.Left + (int) (ts * waveRect.Width);
					int trimEndX = waveRect.Left + (int) (te * waveRect.Width);

					var trimStartRect = new Rectangle(trimStartX - 4, trimBand.Top, 8, TrimHandleHeight);
					var trimEndRect = new Rectangle(trimEndX - 4, trimBand.Top, 8, TrimHandleHeight);

					// Loop toggle
					var loopToggle = new Rectangle(10, y + 120, 100, 18);
					if (loopToggle.Contains(e.Location))
					{
						item.EditorDocument.Flags ^= AudioFlags.Loop;
						Serializer.Write(item.Name, item.EditorDocument);
						Invalidate();
						return;
					}

					// Trim handles
					if (trimStartRect.Contains(e.Location))
					{
						dragMode = DragMode.TrimStart;
						dragItem = item;
						waveformRect = waveRect; // Store for OnMouseMove
						return;
					}
					else if (trimEndRect.Contains(e.Location))
					{
						dragMode = DragMode.TrimEnd;
						dragItem = item;
						waveformRect = waveRect;
						return;
					}

					// Loop handles (only if looping enabled)
					if (item.EditorDocument.Flags.HasFlag(AudioFlags.Loop))
					{
						float ls = item.EditorDocument.LoopStart;
						float le = item.EditorDocument.LoopEnd;

						int trimmedWidth = trimEndX - trimStartX;
						int loopStartX = trimStartX + (int) (ls * trimmedWidth);
						int loopEndX = trimStartX + (int) (le * trimmedWidth);

						var loopStartRect = new Rectangle(loopStartX - 4, loopBand.Top, 8, LoopHandleHeight);
						var loopEndRect = new Rectangle(loopEndX - 4, loopBand.Top, 8, LoopHandleHeight);

						if (loopStartRect.Contains(e.Location))
						{
							dragMode = DragMode.LoopStart;
							dragItem = item;
							waveformRect = waveRect;
							return;
						}
						else if (loopEndRect.Contains(e.Location))
						{
							dragMode = DragMode.LoopEnd;
							dragItem = item;
							waveformRect = waveRect;
							return;
						}
					}
				}

				y += height + 2;
			}
		}

		protected override void OnMouseWheel(MouseEventArgs e)
		{
			_scrollOffset -= e.Delta / 120 * 20; // Smoother scrolling

			int totalHeight = 0;
			for (int i = 0; i < _items.Count; i++)
				totalHeight += (i == _selectedIndex) ? _items[i].ExpandedHeight : _items[i].Collapsed;

			int maxScroll = Math.Max(0, totalHeight - ClientSize.Height);
			_scrollOffset = Math.Max(0, Math.Min(_scrollOffset, maxScroll));

			Invalidate();
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			if (dragMode == DragMode.None || dragItem == null)
				return;

			var doc = dragItem.EditorDocument;

			float t = (float) (e.X - waveformRect.Left) / waveformRect.Width;
			t = Math.Clamp(t, 0f, 1f);

			switch (dragMode)
			{
				case DragMode.TrimStart:
					doc.TrimStart = Math.Min(t, doc.TrimEnd - 0.001f);
					break;

				case DragMode.TrimEnd:
					doc.TrimEnd = Math.Max(t, doc.TrimStart + 0.001f);
					break;

				case DragMode.LoopStart:
				case DragMode.LoopEnd:

					// For loop handles, remap t from trimmed region
					float trimmedWidth = doc.TrimEnd - doc.TrimStart;
					float tInTrimmedRegion = (t - doc.TrimStart) / trimmedWidth;
					tInTrimmedRegion = Math.Clamp(tInTrimmedRegion, 0f, 1f);

					if (dragMode == DragMode.LoopStart)
						doc.LoopStart = Math.Min(tInTrimmedRegion, doc.LoopEnd - 0.001f);
					else
						doc.LoopEnd = Math.Max(tInTrimmedRegion, doc.LoopStart + 0.001f);
					break;
			}

			Invalidate();
		}

		protected override void OnMouseUp(MouseEventArgs e)
		{
			if (dragItem != null)
			{
				Serializer.Write(dragItem.Name, dragItem.EditorDocument);
			}

			dragMode = DragMode.None;
			dragItem = null;
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			var graphics = e.Graphics;

			if (_items.Count == 0)
				return;

			int y = -_scrollOffset;

			graphics.DrawString("Name", Font, Brushes.WhiteSmoke, new Point(ColName, 8));
			graphics.DrawString("Duration", Font, Brushes.WhiteSmoke, new Point(ColDuration, 8));
			graphics.DrawString("Channels", Font, Brushes.WhiteSmoke, new Point(ColChannels, 8));
			graphics.DrawString("Sample Rate", Font, Brushes.WhiteSmoke, new Point(ColSampleRate, 8));

			y += 32;

			for (int i = 0; i < _items.Count; i++)
			{
				var item = _items[i];
				int height = (i == _selectedIndex) ? item.ExpandedHeight : item.Collapsed;

				DrawItem(e.Graphics, item, new Rectangle(0, y, Width, height), i == _selectedIndex, i);

				y += height + 2;
			}
		}

		private void DrawItem(Graphics graphics, SoundItem item, Rectangle rectangle, bool isSelected, int index)
		{

			if ((index % 2) == 0)
			{
				graphics.FillRectangle(_brush1, rectangle);
			}
			else
			{
				graphics.FillRectangle(_brush2, rectangle);
			}

			graphics.FillEllipse(Brushes.White, new Rectangle(rectangle.X + 4, rectangle.Y + 4, 24, 24));

			if (!item.isPlaying)
			{
				DrawPlayIcon(graphics, new Rectangle(rectangle.X + 4, rectangle.Y + 4, 24, 24));
			}
			else
			{
				DrawStopIcon(graphics, new Rectangle(rectangle.X + 4, rectangle.Y + 4, 24, 24));
			}

			graphics.DrawString(item.Name, Font, Brushes.WhiteSmoke, new Point(rectangle.X + ColName, rectangle.Y + 8));
			graphics.DrawString(item.Duration.ToString(@"mm\:ss\.fff"), Font, Brushes.WhiteSmoke, new Point(rectangle.X + ColDuration, rectangle.Y + 8));
			graphics.DrawString(item.Channels.ToString(), Font, Brushes.WhiteSmoke, new Point(rectangle.X + ColChannels, rectangle.Y + 8));
			graphics.DrawString(item.SampleRate.ToString(), Font, Brushes.WhiteSmoke, new Point(rectangle.X + ColSampleRate, rectangle.Y + 8));

			if (isSelected)
			{
				waveformRect = new Rectangle(rectangle.X + 10, rectangle.Y + 52, rectangle.Width - 20, 64);

				Rectangle trimBand = new Rectangle(waveformRect.Left, waveformRect.Top - TrimHandleHeight, waveformRect.Width, TrimHandleHeight);
				Rectangle loopBand = new Rectangle(waveformRect.Left, waveformRect.Bottom, waveformRect.Width, LoopHandleHeight);

				graphics.DrawString("Waveform", Font, Brushes.White, new RectangleF(waveformRect.X, waveformRect.Y - 20, waveformRect.Width, waveformRect.Height));

				graphics.FillRectangle(Brushes.DarkSlateGray, waveformRect);

				DrawWaveform(graphics, waveformRect, _items[index].Waveform);

				float ts = item.EditorDocument.TrimStart;
				float te = item.EditorDocument.TrimEnd;

				int x0 = waveformRect.Left;
				int x1 = waveformRect.Right;

				int trimStartX	= x0 + (int) (ts * waveformRect.Width);
				int trimEndX	= x0 + (int) (te * waveformRect.Width);

				trimStartHandle = new Rectangle(trimStartX - 4, trimBand.Top, 8, TrimHandleHeight);
				trimEndHandle	= new Rectangle(trimEndX - 4, trimBand.Top, 8, TrimHandleHeight);

				float ls = item.EditorDocument.LoopStart;
				float le = item.EditorDocument.LoopEnd;

				int trimmedWidth = trimEndX - trimStartX;

				int loopStartX	= trimStartX + (int) (ls * trimmedWidth);
				int loopEndX	= trimStartX + (int) (le * trimmedWidth);

				loopStartHandle = new Rectangle(loopStartX - 4, loopBand.Top, 8, LoopHandleHeight);
				loopEndHandle = new Rectangle(loopEndX - 4, loopBand.Top, 8, LoopHandleHeight);

				graphics.FillRectangle(Brushes.Orange, trimStartHandle);
				graphics.FillRectangle(Brushes.Orange, trimEndHandle);

				if (item.EditorDocument.Flags.HasFlag(AudioFlags.Loop))
				{
					graphics.FillRectangle(Brushes.Cyan, loopStartHandle);
					graphics.FillRectangle(Brushes.Cyan, loopEndHandle);
				}

				string loopText = item.EditorDocument.Flags.HasFlag(AudioFlags.Loop) ? "Loop: On" : "Loop: Off";

				Brush loopBrush = item.EditorDocument.Flags.HasFlag(AudioFlags.Loop) ? Brushes.LightGreen : Brushes.Gray;

				loopToggleRect = new Rectangle(rectangle.X + 10, rectangle.Y + 120, 100, 18);

				graphics.DrawString(loopText, Font, loopBrush, loopToggleRect.Location);
			}
		}

		private void AutoScrollToItem(int index)
		{
			int y = 0;
			for (int i = 0; i < index; i++)
				y += _items[i].Collapsed + 2;

			int expandHeight = _items[index].ExpandedHeight;
			int viewHeight = this.ClientSize.Height;

			if (y + expandHeight > _scrollOffset + viewHeight)
				_scrollOffset = (y + expandHeight) - viewHeight;

			if (y < _scrollOffset)
				_scrollOffset = y;

			if (_scrollOffset < 0)
				_scrollOffset = 0;
		}

		private void LoadAudio()
		{
			if (!Directory.Exists(EditorPaths.AssetsAudio))
				Directory.CreateDirectory(EditorPaths.AssetsAudio);

			var audioExtensions = new[] { ".wav", ".mp3", ".ogg", ".flac", ".aiff", ".m4a" };

			foreach (var f in Directory.EnumerateFiles(EditorPaths.AssetsAudio))
			{
				string ext = Path.GetExtension(f).ToLowerInvariant();

				// Skip non-audio files (including .gbaud)
				if (!audioExtensions.Contains(ext))
					continue;

				try
				{
					using var reader = new AudioFileReader(f);

					string baseName = Path.GetFileNameWithoutExtension(f);

					AudioEditorDocument editorDoc = null;
					try
					{
						editorDoc = Serializer.Read(baseName);
					}
					catch
					{
						editorDoc = new AudioEditorDocument
						{
							SourcePath = f,
							Flags = AudioFlags.None,
							TrimStart = 0.0f,
							TrimEnd = 1.0f,
							LoopStart = 0.0f,
							LoopEnd = 1.0f
						};
					}

					_items.Add(new SoundItem
					{
						SourcePath = f,
						Name = baseName,
						Duration = reader.TotalTime,
						SampleRate = reader.WaveFormat.SampleRate,
						Channels = reader.WaveFormat.Channels,
						Waveform = GenerateWaveform(f, 4096),
						EditorDocument = editorDoc
					});
				}
				catch (Exception ex)
				{
					Console.WriteLine($"Failed to load {f}: {ex.Message}");
				}
			}
		}

		private void DrawPlayIcon(Graphics g, Rectangle bounds)
		{
			int cx = bounds.X + bounds.Width / 2;
			int cy = bounds.Y + bounds.Height / 2;

			int size = bounds.Width / 3;

			Point[] triangle =
			{
				new Point(cx - size / 2, cy - size),
				new Point(cx - size / 2, cy + size),
				new Point(cx + size,     cy)
			};

			g.FillPolygon(Brushes.Black, triangle);
		}

		private void DrawStopIcon(Graphics g, Rectangle bounds)
		{
			int size = bounds.Width / 3;
			int x = bounds.X + (bounds.Width - size) / 2;
			int y = bounds.Y + (bounds.Height - size) / 2;

			g.FillRectangle(Brushes.Black, new Rectangle(x, y, size, size));
		}

		void PreviewSound(SoundItem item)
		{
			StopAllSounds();

			var reader = new AudioFileReader(item.SourcePath);
			ISampleProvider source = reader;

			if (reader.WaveFormat.Channels == 2)
			{
				source = new StereoToMonoSampleProvider(source)
				{
					LeftVolume = 0.5f,
					RightVolume = 0.5f
				};
			}
			else if (reader.WaveFormat.Channels > 2)
			{
				source = new MultiChannelToMonoSampleProvider(source);
			}

			source = ApplyTrim(source, item.EditorDocument, reader.TotalTime);

			if (item.EditorDocument.Flags.HasFlag(AudioFlags.Loop))
			{
				source = new CachedLoopSampleProvider(source, item.EditorDocument.LoopStart, item.EditorDocument.LoopEnd);
			}

			var waveOut = new WaveOutEvent();
			output = waveOut;
			item.isPlaying = true;

			if (item.EditorDocument.Flags.HasFlag(AudioFlags.Loop))
			{
				var waveProvider = new CachedLoopWaveProvider(source, item.EditorDocument.LoopStart, item.EditorDocument.LoopEnd);

				waveOut.Init(waveProvider);
			}
			else
			{
				waveOut.Init(source.ToWaveProvider());
			}

			waveOut.PlaybackStopped += (_, __) =>
			{
				reader.Dispose();
				waveOut.Dispose();

				if (output == waveOut)
					output = null;

				item.isPlaying = false;
				Invalidate();
			};

			waveOut.Play();
		}

		ISampleProvider ApplyTrim(ISampleProvider source, AudioEditorDocument doc, TimeSpan totalDuration)
		{
			double startSeconds = doc.TrimStart * totalDuration.TotalSeconds;
			double endSeconds	= doc.TrimEnd	* totalDuration.TotalSeconds;

			if (endSeconds <= startSeconds)
				endSeconds = totalDuration.TotalSeconds;

			return new OffsetSampleProvider(source)
			{
				SkipOver = TimeSpan.FromSeconds(startSeconds),
				Take = TimeSpan.FromSeconds(endSeconds - startSeconds)
			};
		}

		void StopAllSounds()
		{
			if (output != null)
			{
				output.Stop();
				output = null;
			}

			foreach (var i in _items)
				i.isPlaying = false;
		}

		static List<(float min, float max)> GenerateWaveform(string filePath, int resolution = 4096)
		{
			var result = new List<(float min, float max)>(resolution);

			using var reader = new AudioFileReader(filePath);

			long totalSamples =
				reader.Length / sizeof(float) / reader.WaveFormat.Channels;

			int channels = reader.WaveFormat.Channels;
			long samplesPerPixel = totalSamples / resolution;

			float[] buffer = new float[4096];
			long samplesAccumulated = 0;

			float min = 1f;
			float max = -1f;

			int read;
			while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
			{
				for (int i = 0; i < read; i += channels)
				{
					float sample = buffer[i]; // left / mono

					min = Math.Min(min, sample);
					max = Math.Max(max, sample);
					samplesAccumulated++;

					if (samplesAccumulated >= samplesPerPixel)
					{
						result.Add((min, max));
						min = 1f;
						max = -1f;
						samplesAccumulated = 0;

						if (result.Count >= resolution)
							return result;
					}
				}
			}

			// pad if short
			while (result.Count < resolution)
				result.Add((0, 0));

			return result;
		}

		// Downsample when drawing to fit the available rect width
		void DrawWaveform(Graphics g, Rectangle rect, List<(float min, float max)> data)
		{
			if (data == null || data.Count == 0)
				return;

			int midY = rect.Top + rect.Height / 2;
			float scaleY = rect.Height / 2f;
			int targetWidth = rect.Width;

			// If the waveform data has more samples than pixels, downsample
			if (data.Count > targetWidth)
			{
				float samplesPerPixel = (float) data.Count / targetWidth;

				for (int x = 0; x < targetWidth; x++)
				{
					int startIdx = (int) (x * samplesPerPixel);
					int endIdx = (int) ((x + 1) * samplesPerPixel);

					// Find min/max across this range
					float rangeMin = 1f;
					float rangeMax = -1f;

					for (int i = startIdx; i < endIdx && i < data.Count; i++)
					{
						rangeMin = Math.Min(rangeMin, data[i].min);
						rangeMax = Math.Max(rangeMax, data[i].max);
					}

					int y1 = midY - (int) (rangeMax * scaleY);
					int y2 = midY - (int) (rangeMin * scaleY);

					g.DrawLine(Pens.LightGreen,
						rect.Left + x,
						y1,
						rect.Left + x,
						y2);
				}
			}
			// If waveform data has fewer samples than pixels, stretch it
			else
			{
				float pixelsPerSample = (float) targetWidth / data.Count;

				for (int i = 0; i < data.Count; i++)
				{
					var (min, max) = data[i];

					int x1 = rect.Left + (int) (i * pixelsPerSample);
					int x2 = rect.Left + (int) ((i + 1) * pixelsPerSample);

					int y1 = midY - (int) (max * scaleY);
					int y2 = midY - (int) (min * scaleY);

					// Draw a vertical bar for each sample, stretched horizontally
					for (int x = x1; x < x2 && x < rect.Right; x++)
					{
						g.DrawLine(Pens.LightGreen, x, y1, x, y2);
					}
				}
			}
		}
	}

	internal class SoundItem
	{
		public int ExpandedHeight => 140;
		public int Collapsed => 32;

		public string SourcePath { get; set; }
		public string Name { get; set; }
		public TimeSpan Duration { get; set; }
		public int SampleRate { get; set; }
		public int Channels { get; set; }
		public bool isPlaying { get; set; }
		public List<(float min, float max)> Waveform { get; set; }

		public AudioEditorDocument EditorDocument { get; set; }
	}

	internal enum DragMode
	{
		None,
		TrimStart,
		TrimEnd,
		LoopStart,
		LoopEnd
	}
}

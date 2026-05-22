using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Echo.Data;

public sealed class WinMMAudioPlayer : IDisposable
{
	private struct WAVEFORMATEX
	{
		public ushort wFormatTag;

		public ushort nChannels;

		public uint nSamplesPerSec;

		public uint nAvgBytesPerSec;

		public ushort nBlockAlign;

		public ushort wBitsPerSample;

		public ushort cbSize;
	}

	private struct WAVEHDR
	{
		public nint lpData;

		public uint dwBufferLength;

		public uint dwBytesRecorded;

		public nint dwUser;

		public uint dwFlags;

		public uint dwLoops;

		public nint lpNext;

		public nint reserved;
	}

	private const int WAVE_MAPPER = -1;

	private const uint WHDR_DONE = 1u;

	private const uint WHDR_PREPARED = 2u;

	private const int BUFFER_SAMPLES = 2048;

	private const int NUM_BUFFERS = 2;

	private nint _hWaveOut = IntPtr.Zero;

	private WAVEHDR[] _headers = new WAVEHDR[2];

	private GCHandle[] _dataHandles = new GCHandle[2];

	private short[][] _buffers = new short[2][];

	private sbyte[] _samples = Array.Empty<sbyte>();

	private int _position;

	private int _trimStart;

	private int _trimEnd;

	private int _loopStart;

	private int _loopEnd;

	private bool _loop;

	private bool _playing;

	private bool _disposed;

	private Thread? _feedThread;

	private readonly object _lock = new object();

	public bool IsPlaying => _playing;

	public event EventHandler? PlaybackStopped;

	[DllImport("winmm.dll")]
	private static extern int waveOutOpen(out nint hWaveOut, int uDeviceID, ref WAVEFORMATEX lpFormat, nint dwCallback, nint dwInstance, uint fdwOpen);

	[DllImport("winmm.dll")]
	private static extern int waveOutClose(nint hWaveOut);

	[DllImport("winmm.dll")]
	private static extern int waveOutPrepareHeader(nint hWaveOut, ref WAVEHDR lpWaveOutHdr, uint uSize);

	[DllImport("winmm.dll")]
	private static extern int waveOutUnprepareHeader(nint hWaveOut, ref WAVEHDR lpWaveOutHdr, uint uSize);

	[DllImport("winmm.dll")]
	private static extern int waveOutWrite(nint hWaveOut, ref WAVEHDR lpWaveOutHdr, uint uSize);

	[DllImport("winmm.dll")]
	private static extern int waveOutReset(nint hWaveOut);

	public void Play(AudioDocument doc)
	{
		Stop();
		lock (_lock)
		{
			_samples = doc.Samples;
			_trimStart = doc.TrimStart;
			_trimEnd = doc.TrimEnd;
			_loopStart = doc.LoopStart;
			_loopEnd = doc.LoopEnd;
			_loop = doc.LoopEnabled;
			_position = _trimStart;
			_playing = true;
		}
		OpenDevice(doc.SampleRate);
		StartFeedThread();
	}

	public void Stop()
	{
		lock (_lock)
		{
			_playing = false;
		}
		_feedThread?.Join(500);
		_feedThread = null;
		CloseDevice();
	}

	public void Dispose()
	{
		if (!_disposed)
		{
			_disposed = true;
			Stop();
		}
	}

	private void OpenDevice(int sampleRate)
	{
		WAVEFORMATEX lpFormat = new WAVEFORMATEX
		{
			wFormatTag = 1,
			nChannels = 1,
			nSamplesPerSec = (uint)sampleRate,
			wBitsPerSample = 16,
			nBlockAlign = 2,
			nAvgBytesPerSec = (uint)(sampleRate * 2),
			cbSize = 0
		};
		waveOutOpen(out _hWaveOut, -1, ref lpFormat, IntPtr.Zero, IntPtr.Zero, 0u);
		for (int i = 0; i < 2; i++)
		{
			_buffers[i] = new short[2048];
			_dataHandles[i] = GCHandle.Alloc(_buffers[i], GCHandleType.Pinned);
			_headers[i] = new WAVEHDR
			{
				lpData = _dataHandles[i].AddrOfPinnedObject(),
				dwBufferLength = 4096u
			};
			waveOutPrepareHeader(_hWaveOut, ref _headers[i], (uint)Marshal.SizeOf<WAVEHDR>());
			FillBuffer(i);
			waveOutWrite(_hWaveOut, ref _headers[i], (uint)Marshal.SizeOf<WAVEHDR>());
		}
	}

	private void CloseDevice()
	{
		if (_hWaveOut == IntPtr.Zero)
		{
			return;
		}
		waveOutReset(_hWaveOut);
		for (int i = 0; i < 2; i++)
		{
			waveOutUnprepareHeader(_hWaveOut, ref _headers[i], (uint)Marshal.SizeOf<WAVEHDR>());
			if (_dataHandles[i].IsAllocated)
			{
				_dataHandles[i].Free();
			}
		}
		waveOutClose(_hWaveOut);
		_hWaveOut = IntPtr.Zero;
	}

	private void StartFeedThread()
	{
		_feedThread = new Thread(FeedLoop)
		{
			IsBackground = true,
			Name = "Echo WinMM Feed"
		};
		_feedThread.Start();
	}

	private void FeedLoop()
	{
		int num = 0;
		while (true)
		{
			lock (_lock)
			{
				if (!_playing)
				{
					break;
				}
			}
			if ((_headers[num].dwFlags & 1) != 0)
			{
				bool playing;
				lock (_lock)
				{
					playing = _playing;
					if (playing)
					{
						FillBuffer(num);
					}
				}
				if (!playing)
				{
					break;
				}
				waveOutWrite(_hWaveOut, ref _headers[num], (uint)Marshal.SizeOf<WAVEHDR>());
				num = (num + 1) % 2;
			}
			else
			{
				Thread.Sleep(1);
			}
		}
		lock (_lock)
		{
			_playing = false;
		}
		this.PlaybackStopped?.Invoke(this, EventArgs.Empty);
	}

	private void FillBuffer(int bufferIndex)
	{
		short[] array = _buffers[bufferIndex];
		for (int i = 0; i < 2048; i++)
		{
			if (_position > _trimEnd)
			{
				if (!_loop)
				{
					for (int j = i; j < 2048; j++)
					{
						array[j] = 0;
					}
					_playing = false;
					break;
				}
				_position = _loopStart;
			}
			array[i] = (short)(_samples[_position] << 8);
			_position++;
			if (_loop && _position >= _loopEnd)
			{
				_position = _loopStart;
			}
		}
	}
}

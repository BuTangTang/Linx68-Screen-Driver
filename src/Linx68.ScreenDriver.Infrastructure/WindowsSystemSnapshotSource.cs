using System;
using System.ComponentModel;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Linx68.ScreenDriver.Application;
using Linx68.ScreenDriver.Core;

namespace Linx68.ScreenDriver.Infrastructure;

public sealed class WindowsSystemSnapshotSource : ISystemSnapshotSource
{
	private readonly struct FileTime
	{
		public readonly uint Low;

		public readonly uint High;
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
	private struct MemoryStatusEx
	{
		public uint Length;

		public uint MemoryLoad;

		public ulong TotalPhysical;

		public ulong AvailablePhysical;

		public ulong TotalPageFile;

		public ulong AvailablePageFile;

		public ulong TotalVirtual;

		public ulong AvailableVirtual;

		public ulong AvailableExtendedVirtual;

		public MemoryStatusEx()
		{
			this = default(MemoryStatusEx);
			Length = (uint)Marshal.SizeOf<MemoryStatusEx>();
		}
	}

	private readonly object _sync = new object();

	private ulong? _previousIdle;

	private ulong? _previousTotal;

	private long? _previousReceived;

	private long? _previousSent;

	private DateTimeOffset? _previousNetworkAt;

	public ValueTask<SystemSnapshot> ReadAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		cancellationToken.ThrowIfCancellationRequested();
		double cpuPercent = ReadCpuPercent();
		double memoryPercent = ReadMemoryPercent();
		DateTimeOffset now = DateTimeOffset.Now;
		var (downloadMbps, uploadMbps) = ReadNetworkMbps(now);
		return ValueTask.FromResult(new SystemSnapshot(now, cpuPercent, memoryPercent, downloadMbps, uploadMbps));
	}

	private (double Download, double Upload) ReadNetworkMbps(DateTimeOffset now)
	{
		long num = 0L;
		long num2 = 0L;
		NetworkInterface[] allNetworkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
		foreach (NetworkInterface networkInterface in allNetworkInterfaces)
		{
			if (networkInterface.OperationalStatus == OperationalStatus.Up && networkInterface.NetworkInterfaceType != NetworkInterfaceType.Loopback)
			{
				IPv4InterfaceStatistics iPv4Statistics = networkInterface.GetIPv4Statistics();
				num += iPv4Statistics.BytesReceived;
				num2 += iPv4Statistics.BytesSent;
			}
		}
		lock (_sync)
		{
			if (_previousReceived is long previousReceived
				&& _previousSent is long previousSent
				&& _previousNetworkAt is DateTimeOffset previousNetworkAt)
			{
				double elapsedSeconds = Math.Max((now - previousNetworkAt).TotalSeconds, 0.001);
				double download = Math.Max(0L, num - previousReceived) * 8.0 / 1_000_000.0 / elapsedSeconds;
				double upload = Math.Max(0L, num2 - previousSent) * 8.0 / 1_000_000.0 / elapsedSeconds;
				_previousReceived = num;
				_previousSent = num2;
				_previousNetworkAt = now;
				return (Download: download, Upload: upload);
			}
			_previousReceived = num;
			_previousSent = num2;
			_previousNetworkAt = now;
			return (Download: 0.0, Upload: 0.0);
		}
	}

	private double ReadCpuPercent()
	{
		if (!GetSystemTimes(out var idleTime, out var kernelTime, out var userTime))
		{
			throw new Win32Exception(Marshal.GetLastWin32Error());
		}
		ulong num = ToUInt64(idleTime);
		ulong num2 = ToUInt64(kernelTime) + ToUInt64(userTime);
		lock (_sync)
		{
			if (_previousIdle is ulong previousIdle && _previousTotal is ulong previousTotal)
			{
				ulong idleDelta = num - previousIdle;
				ulong totalDelta = num2 - previousTotal;
				_previousIdle = num;
				_previousTotal = num2;
				if (totalDelta == 0L)
				{
					return 0.0;
				}
				return Math.Clamp(100.0 * (totalDelta - idleDelta) / totalDelta, 0.0, 100.0);
			}
			_previousIdle = num;
			_previousTotal = num2;
			return 0.0;
		}
	}

	private static double ReadMemoryPercent()
	{
		MemoryStatusEx buffer = new MemoryStatusEx();
		if (!GlobalMemoryStatusEx(ref buffer))
		{
			throw new Win32Exception(Marshal.GetLastWin32Error());
		}
		return buffer.MemoryLoad;
	}

	private static ulong ToUInt64(FileTime value)
	{
		return ((ulong)value.High << 32) | value.Low;
	}

	[DllImport("kernel32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool GetSystemTimes(out FileTime idleTime, out FileTime kernelTime, out FileTime userTime);

	[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);
}

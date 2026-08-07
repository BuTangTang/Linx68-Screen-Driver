using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Linx68.ScreenDriver.Application;
using Linx68.ScreenDriver.Core;

namespace Linx68.ScreenDriver.Infrastructure;

public sealed class JsonSettingsStore : ISettingsStore
{
	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		WriteIndented = true,
		PropertyNameCaseInsensitive = true
	};

	public string Path { get; }

	public JsonSettingsStore(string? path = null)
	{
		Path = path ?? ResolveDefaultPath();
	}

	private static string ResolveDefaultPath()
	{
		if (File.Exists(System.IO.Path.Combine(AppContext.BaseDirectory, "portable.flag")))
		{
			return System.IO.Path.Combine(AppContext.BaseDirectory, "Data", "settings.json");
		}

		string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
		string currentPath = System.IO.Path.Combine(localAppData, "Linx68ScreenDriver", "settings.json");
		string[] legacyPaths =
		[
			System.IO.Path.Combine(localAppData, "Linx68ScreenManager", "settings.json"),
			System.IO.Path.Combine(localAppData, "KeyboardScreenStudio", "settings.json")
		];
		string? legacyPath = legacyPaths.FirstOrDefault(File.Exists);
		if (!File.Exists(currentPath) && legacyPath is not null)
		{
			try
			{
				Directory.CreateDirectory(System.IO.Path.GetDirectoryName(currentPath)!);
				File.Copy(legacyPath, currentPath, overwrite: false);
			}
			catch
			{
				return legacyPath;
			}
		}
		return currentPath;
	}

	public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
	{
		try
		{
			if (!File.Exists(Path))
			{
				return new AppSettings();
			}

			await using FileStream stream = File.OpenRead(Path);
			AppSettings settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions, cancellationToken)
				?? new AppSettings();
			return Normalize(settings);
		}
		catch (JsonException)
		{
			PreserveInvalidSettings();
			return new AppSettings();
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			return new AppSettings();
		}
	}

	public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(settings);
		Normalize(settings);
		string? directoryName = System.IO.Path.GetDirectoryName(Path);
		if (!string.IsNullOrWhiteSpace(directoryName))
		{
			Directory.CreateDirectory(directoryName);
		}

		string temporaryPath = $"{Path}.{Guid.NewGuid():N}.tmp";
		try
		{
			await using (var stream = new FileStream(
				temporaryPath,
				FileMode.CreateNew,
				FileAccess.Write,
				FileShare.None,
				bufferSize: 4096,
				FileOptions.Asynchronous | FileOptions.WriteThrough))
			{
				await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken);
				await stream.FlushAsync(cancellationToken);
			}

			File.Move(temporaryPath, Path, overwrite: true);
		}
		finally
		{
			if (File.Exists(temporaryPath))
			{
				File.Delete(temporaryPath);
			}
		}
	}

	private static AppSettings Normalize(AppSettings settings)
	{
		settings.SettingsVersion = AppSettings.CurrentSettingsVersion;
		settings.DeviceEndpoint ??= string.Empty;
		settings.SelectedThemeId ??= "clock-dot-matrix";
		settings.AccentColor ??= "#E4694C";
		settings.SelectedFontId ??= "builtin:segoe-variable-display";
		settings.MediaPlayingThemeId ??= "music";
		settings.MediaIdleThemeId ??= "system";
		settings.SafeArea ??= new ScreenInsets(10, 52, 10, 12);
		settings.Music ??= new MusicSettings();
		settings.AiQuota ??= new AiQuotaSettings();
		settings.Weather ??= new WeatherSettings();
		settings.Stocks ??= new StockSettings();
		settings.RefreshSeconds = Math.Clamp(settings.RefreshSeconds, 1, 30);
		return settings;
	}

	private void PreserveInvalidSettings()
	{
		try
		{
			if (!File.Exists(Path))
			{
				return;
			}

			string backupPath = $"{Path}.invalid-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}.json";
			File.Copy(Path, backupPath, overwrite: false);
		}
		catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
		{
			// Loading still falls back to defaults when the invalid file cannot be preserved.
		}
	}
}

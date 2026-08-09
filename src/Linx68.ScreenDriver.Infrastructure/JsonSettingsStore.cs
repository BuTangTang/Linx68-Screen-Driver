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

			AppSettings settings;
			await using (FileStream stream = File.OpenRead(Path))
			{
				settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions, cancellationToken).ConfigureAwait(false)
					?? new AppSettings();
			}

			int loadedVersion = settings.SettingsVersion;
			settings = Normalize(settings);
			if (loadedVersion < AppSettings.CurrentSettingsVersion)
			{
				try
				{
					await SaveAsync(settings, cancellationToken).ConfigureAwait(false);
				}
				catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
				{
					// Keep using the migrated settings even if the original file cannot be replaced.
				}
			}

			return settings;
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
				await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken).ConfigureAwait(false);
				await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
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
		bool migrateLegacyQuotaSettings = settings.SettingsVersion < 10;
		bool migrateRemovedMusicThemes = settings.SettingsVersion < 5;
		bool enableOnlineLyrics = settings.SettingsVersion < 7;
		settings.SettingsVersion = AppSettings.CurrentSettingsVersion;
		settings.DeviceEndpoint ??= string.Empty;
		settings.SelectedThemeId ??= "clock-weather";
		settings.AccentColor ??= "#E4694C";
		settings.SelectedFontId ??= "builtin:segoe-variable-display";
		settings.SafeArea ??= new ScreenInsets(10, 52, 10, 12);
		settings.Music ??= new MusicSettings();
		if (enableOnlineLyrics)
		{
			settings.Music.EnableOnlineLyrics = true;
		}
		settings.AiQuota ??= new AiQuotaSettings();
		if (migrateLegacyQuotaSettings || string.IsNullOrWhiteSpace(settings.AiQuota.DisplayName))
		{
			settings.AiQuota.DisplayName = "Codex";
		}
		if (migrateRemovedMusicThemes)
		{
			settings.SelectedThemeId = NormalizeRemovedMusicTheme(settings.SelectedThemeId, "music");
		}
		settings.SelectedThemeId = BuiltInThemes.NormalizeThemeId(settings.SelectedThemeId) ?? "clock-weather";
		if (!Enum.IsDefined(settings.ScreenColorMode))
		{
			settings.ScreenColorMode = ScreenColorMode.Night;
		}
		settings.Weather ??= new WeatherSettings();
		settings.RefreshSeconds = Math.Clamp(settings.RefreshSeconds, 1, 30);
		return settings;
	}

	private static string NormalizeRemovedMusicTheme(string? themeId, string replacement) =>
		themeId is "music-vinyl" or "music-cassette" or "music-minimal" or "music-poster"
			? replacement
			: themeId ?? replacement;

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

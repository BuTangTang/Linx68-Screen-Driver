using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Media;
using Linx68.ScreenDriver.Core;

namespace Linx68.ScreenDriver.Infrastructure;

public sealed class FontFolderCatalog : IDisposable
{
	private static readonly HashSet<string> SupportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".ttf", ".otf" };

	private readonly FileSystemWatcher _watcher;

	private readonly Timer _debounceTimer;

	private bool _disposed;

	public string FolderPath { get; }

	public event EventHandler? FontsChanged;

	public FontFolderCatalog(string folderPath)
	{
		FolderPath = Path.GetFullPath(folderPath);
		Directory.CreateDirectory(FolderPath);
		_debounceTimer = new Timer(delegate
		{
			RaiseFontsChanged();
		}, null, -1, -1);
		_watcher = new FileSystemWatcher(FolderPath)
		{
			IncludeSubdirectories = false,
			NotifyFilter = (NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite | NotifyFilters.CreationTime),
			EnableRaisingEvents = true
		};
		_watcher.Created += OnFileChanged;
		_watcher.Changed += OnFileChanged;
		_watcher.Deleted += OnFileChanged;
		_watcher.Renamed += OnFileChanged;
	}

	public IReadOnlyList<ScreenFontOption> Scan()
	{
		List<ScreenFontOption> list = new List<ScreenFontOption> { ScreenFontOption.Default };
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ScreenFontOption.Default.Id };
		Uri baseUri = new Uri(EnsureTrailingSeparator(FolderPath), UriKind.Absolute);
		foreach (string item in (from path in Directory.EnumerateFiles(FolderPath)
			where SupportedExtensions.Contains(Path.GetExtension(path))
			select path).OrderBy<string, string>(Path.GetFileName, StringComparer.CurrentCultureIgnoreCase))
		{
			try
			{
				string localizedName = GetLocalizedName(new GlyphTypeface(new Uri(item, UriKind.Absolute)).Win32FamilyNames);
				if (!string.IsNullOrWhiteSpace(localizedName))
				{
					string fileName = Path.GetFileName(item);
					string text = "file:" + fileName.ToLowerInvariant() + "|" + localizedName.ToLowerInvariant();
					if (hashSet.Add(text))
					{
						FontFamily fontFamily = new FontFamily(baseUri, "./#" + localizedName);
						list.Add(new ScreenFontOption(text, localizedName, localizedName, fileName, fontFamily));
					}
				}
			}
			catch (Exception ex) when (((ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException) ? 1 : 0) != 0)
			{
			}
		}
		return list;
	}

	public void Dispose()
	{
		if (!_disposed)
		{
			_disposed = true;
			_watcher.EnableRaisingEvents = false;
			_watcher.Dispose();
			_debounceTimer.Dispose();
		}
	}

	private void OnFileChanged(object sender, FileSystemEventArgs e)
	{
		if (SupportedExtensions.Contains(Path.GetExtension(e.FullPath)))
		{
			_debounceTimer.Change(350, -1);
		}
	}

	private void RaiseFontsChanged()
	{
		if (!_disposed)
		{
			this.FontsChanged?.Invoke(this, EventArgs.Empty);
		}
	}

	private static string GetLocalizedName(IDictionary<CultureInfo, string> names)
	{
		if (names.TryGetValue(CultureInfo.CurrentUICulture, out string? value))
		{
			return value;
		}
		KeyValuePair<CultureInfo, string> keyValuePair = names.FirstOrDefault<KeyValuePair<CultureInfo, string>>((KeyValuePair<CultureInfo, string> pair) => pair.Key.TwoLetterISOLanguageName == CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
		if (!string.IsNullOrWhiteSpace(keyValuePair.Value))
		{
			return keyValuePair.Value;
		}
		if (names.TryGetValue(CultureInfo.GetCultureInfo("en-US"), out string? value2))
		{
			return value2;
		}
		return names.Values.FirstOrDefault() ?? string.Empty;
	}

	private static string EnsureTrailingSeparator(string path)
	{
		if (!path.EndsWith(Path.DirectorySeparatorChar))
		{
			ReadOnlySpan<char> readOnlySpan = path;
			char reference = Path.DirectorySeparatorChar;
			return string.Concat(readOnlySpan, new ReadOnlySpan<char>(ref reference));
		}
		return path;
	}
}

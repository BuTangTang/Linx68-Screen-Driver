# Privacy

灵犀68屏幕驱动 (Linx68 Screen Driver) does not include first-party analytics, advertising,
telemetry, or a project-operated cloud service.

The application processes system usage, Windows media-session information,
selected local images, device addresses, cities, stock symbols, and display
preferences on the local computer. Portable builds store settings under
`Data/settings.json`.

Optional online features contact third-party services directly:

- When automatic weather location is enabled, the app asks the Windows location service for an approximate position. The current coordinates are sent directly from the app to BigDataCloud to resolve the city name and to Open-Meteo for the weather request; coordinates are not saved in `settings.json`. When automatic location is disabled or unavailable, only the configured city query is sent to Open-Meteo.
- Stock symbols are sent to an unofficial Yahoo Finance web endpoint. This integration is experimental, may become unavailable, and must not be relied on for investment decisions.
- Xiaomi MiMo quota information is read inside a WebView2 session after the
  user signs in to Xiaomi's website. This integration is experimental and is
  not an official Xiaomi integration. The platform may change or restrict this access at any time.
- Online synchronized lyrics are disabled by default. When enabled for a dynamic music theme, the current track title, artist, and duration are sent directly to LRCLIB. Matching lyrics are cached in memory for the current application session and are not written to the settings file.

WebView2 sign-in state, including cookies and browser storage, is saved locally
under `Data/MiMoWebView2` in portable mode or under the application's local
application-data directory in installed/development mode. Do not share or
publish that folder. Delete it while the application is closed to remove the
locally saved Xiaomi sign-in state.

Images sent to the keyboard are posted directly from the computer to the
configured device address over unencrypted HTTP. Use this feature only on a
trusted local network.

The application does not intentionally transmit settings, media information,
system metrics, local image paths, or Xiaomi credentials to the project author.
Third-party services process requests according to their own privacy policies
and terms.

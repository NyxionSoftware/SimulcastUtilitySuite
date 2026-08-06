using SimulcastUtility.Plugins.Interfaces;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SimulcastUtility.Wpf.Services
{
    public sealed class WpfPluginBrandingManager : IPluginBrandingManager
    {
        private const string ApplicationLogoResourceKey = "ApplicationLogoImageSource";
        private readonly Dictionary<Guid, ImageSource> _pluginLogos = new();
        private object? _defaultLogoSource;
        private ImageSource? _defaultWindowIcon;
        private bool _hasCapturedDefault;

        public async Task SetApplicationLogoAsync(Guid pluginIdentifier, Uri logoUri, CancellationToken cancellationToken = default)
        {
            if (pluginIdentifier == Guid.Empty)
                throw new ArgumentException("A plugin identifier is required.", nameof(pluginIdentifier));

            ArgumentNullException.ThrowIfNull(logoUri);
            cancellationToken.ThrowIfCancellationRequested();

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                CaptureDefaultLogo();
                Uri resolvedLogoUri = ResolveLogoUri(logoUri);
                BitmapImage logo = new();
                logo.BeginInit();
                logo.CacheOption = BitmapCacheOption.OnLoad;
                logo.UriSource = resolvedLogoUri;
                logo.EndInit();
                logo.Freeze();

                // Reinsert an existing owner so its latest request becomes the active override.
                _pluginLogos.Remove(pluginIdentifier);
                _pluginLogos[pluginIdentifier] = logo;
                ApplyLogo(logo);
            });
        }

        private static Uri ResolveLogoUri(Uri logoUri)
        {
            if (logoUri.IsAbsoluteUri)
                return logoUri;

            string componentPath = logoUri.OriginalString.TrimStart('/');
            return new Uri($"pack://application:,,,/{componentPath}", UriKind.Absolute);
        }

        public async Task RemoveApplicationLogoAsync(Guid pluginIdentifier, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (!_pluginLogos.Remove(pluginIdentifier))
                    return;

                if (_pluginLogos.Count > 0)
                    ApplyLogo(_pluginLogos.Last().Value);
                else if (_hasCapturedDefault && _defaultLogoSource is not null)
                {
                    System.Windows.Application.Current.Resources[ApplicationLogoResourceKey] = _defaultLogoSource;
                    SetMainWindowIcon(_defaultWindowIcon);
                }
                else
                {
                    System.Windows.Application.Current.Resources.Remove(ApplicationLogoResourceKey);
                    SetMainWindowIcon(_defaultWindowIcon);
                }
            });
        }

        private static void ApplyLogo(ImageSource logo)
        {
            System.Windows.Application.Current.Resources[ApplicationLogoResourceKey] = logo;
            SetMainWindowIcon(logo);
        }

        private static void SetMainWindowIcon(ImageSource? icon)
        {
            if (System.Windows.Application.Current.MainWindow is not null)
                System.Windows.Application.Current.MainWindow.Icon = icon;
        }

        private void CaptureDefaultLogo()
        {
            if (_hasCapturedDefault)
                return;

            _defaultLogoSource = System.Windows.Application.Current.TryFindResource(ApplicationLogoResourceKey);
            _defaultWindowIcon = System.Windows.Application.Current.MainWindow?.Icon;
            _hasCapturedDefault = true;
        }
    }
}

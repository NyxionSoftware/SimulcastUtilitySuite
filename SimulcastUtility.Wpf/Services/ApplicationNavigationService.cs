using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace SimulcastUtility.Wpf.Services
{
    public sealed class ApplicationNavigationService
    {
        private readonly Stack<NavigationEntry> _history = new();
        private ContentControl? _host;
        private NavigationEntry? _current;
        private UIElement? _fallbackPage;

        public void Initialize(ContentControl host, UIElement fallbackPage)
        {
            _host = host;
            _fallbackPage = fallbackPage;
            _current = new NavigationEntry(fallbackPage, null);
            host.Content = fallbackPage;
        }

        public void NavigateTo(UIElement page, bool slideFromRight = true, Guid? pluginIdentifier = null)
        {
            ArgumentNullException.ThrowIfNull(page);
            ContentControl host = GetHost();

            if (_current?.TryGetPage(out UIElement? currentPage) == true && ReferenceEquals(currentPage, page))
                return;

            if (_current is not null)
                _history.Push(_current);

            _current = new NavigationEntry(page, pluginIdentifier);
            SetContent(host, page, slideFromRight);
        }

        public void NavigateBack()
        {
            ContentControl host = GetHost();

            while (_history.TryPop(out NavigationEntry? entry))
            {
                if (!entry.IsValid || !entry.TryGetPage(out UIElement? page) || page is null)
                    continue;

                _current = entry;
                SetContent(host, page, slideFromRight: false);
                return;
            }

            NavigateToFallback(clearHistory: true);
        }

        public void NavigateToFallback(bool clearHistory)
        {
            ContentControl host = GetHost();
            UIElement fallbackPage = _fallbackPage ?? throw new InvalidOperationException("The navigation fallback page has not been initialized.");

            if (clearHistory)
                _history.Clear();
            else if (_current is not null && _current.TryGetPage(out UIElement? currentPage) && !ReferenceEquals(currentPage, fallbackPage))
                _history.Push(_current);

            _current = new NavigationEntry(fallbackPage, null);
            SetContent(host, fallbackPage, slideFromRight: false);
        }

        public void RemovePluginPages(Guid pluginIdentifier)
        {
            foreach (NavigationEntry entry in _history)
            {
                if (entry.PluginIdentifier == pluginIdentifier)
                    entry.Invalidate();
            }

            if (_current?.PluginIdentifier != pluginIdentifier)
                return;

            _current.Invalidate();
            NavigateBack();
        }

        private static void SetContent(ContentControl host, UIElement page, bool slideFromRight)
        {
            host.Content = page;
            double distance = Math.Max(host.ActualWidth, 300);
            TranslateTransform transform = new(slideFromRight ? distance : -distance, 0);
            page.RenderTransform = transform;
            DoubleAnimation animation = new()
            {
                From = transform.X,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(260),
                EasingFunction = new CubicEase() { EasingMode = EasingMode.EaseOut }
            };
            transform.BeginAnimation(TranslateTransform.XProperty, animation);
        }

        private ContentControl GetHost()
        {
            return _host ?? throw new InvalidOperationException("Application navigation has not been initialized.");
        }

        private sealed class NavigationEntry
        {
            private UIElement? _page;

            public Guid? PluginIdentifier { get; }

            public bool IsValid { get; private set; } = true;

            public NavigationEntry(UIElement page, Guid? pluginIdentifier)
            {
                _page = page;
                PluginIdentifier = pluginIdentifier;
            }

            public bool TryGetPage(out UIElement? page)
            {
                page = _page;
                return page is not null;
            }

            public void Invalidate()
            {
                IsValid = false;
                _page = null;
            }
        }
    }
}

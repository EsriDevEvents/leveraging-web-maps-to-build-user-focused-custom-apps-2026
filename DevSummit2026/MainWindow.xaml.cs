using Esri.ArcGISRuntime.Data;
using Esri.ArcGISRuntime.Geotriggers;
using Esri.ArcGISRuntime.Mapping;
using System.Windows;
using Esri.ArcGISRuntime.Mapping.Popups;

using Esri.ArcGISRuntime.Toolkit.UI.Controls;

namespace DevSummit2026
{
    public partial class MainWindow : Window
    {
        private const string _webmap =
            @"https://runtime.maps.arcgis.com/home/item.html?id=1b630c47c9a14ee9a312204a389796b4";

        private GeotriggerMonitor? _monitor;
        private SimulatedLocationDataSource? _simulatedSource;

        private bool _isPlaying;

        public MainWindow()
        {
            InitializeComponent();
            _ = InitializeAsync();
            _speedText.Text = $"{_speedSlider.Value:0.##}x";
        }

        private async Task InitializeAsync()
        {
            // Create the map from the web map
            var map = new Map(new Uri(_webmap));

            // Load the map to retrieve metadata
            await map.LoadAsync();

            // Make the app title the same as the web map title
            if (map.Item != null)
                Title = map.Item.Title;

            // Create the simulated location source
            _simulatedSource = SimulatedLocationDataSource.Create(_mapView);

            // Show blue dot using this source
            _mapView.LocationDisplay.DataSource = _simulatedSource;
            _mapView.LocationDisplay.IsEnabled = true;

            // Pre-start the data source so Play/Pause works immediately (starts paused by default)
            await _simulatedSource.StartAsync();

            // Load web map geotriggers authored in the map
            await map.GeotriggersInfo.LoadAsync();
            var geotrigger = map.GeotriggersInfo.Geotriggers.FirstOrDefault();

            if (geotrigger != null)
            {
                // Wire the geotrigger feed to our simulated source
                if (geotrigger.Feed is LocationGeotriggerFeed locationFeed)
                    locationFeed.LocationDataSource = _simulatedSource;

                // Start monitoring
                _monitor = new GeotriggerMonitor(geotrigger);
                _monitor.Notification += Geotrigger_Notification;
                _ = _monitor.StartAsync();
            }

            _mapView.Map = map;

            InitializeCaptureUi();
        }

        private void PlayPause_Click(object sender, RoutedEventArgs e)
        {
            if (_simulatedSource == null) return;

            // Don’t allow playback while in capture mode
            if (_simulatedSource.IsCaptureModeEnabled)
                return;

            _isPlaying = !_isPlaying;

            if (_isPlaying)
            {
                _simulatedSource.Play();
                _playPauseButton.Content = "Pause";
            }
            else
            {
                _simulatedSource.Pause();
                _playPauseButton.Content = "Play";
            }
        }

        private void InitializeCaptureUi()
        {
            if (_simulatedSource == null) return;

            _simulatedSource.CaptureCountChanged += (_, count) =>
            {
                Dispatcher.Invoke(() => _captureCountText.Text = count.ToString());
            };
        }

        private void SpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_simulatedSource == null) return;

            _simulatedSource.SpeedMultiplier = _speedSlider.Value;
            _speedText.Text = $"{_speedSlider.Value:0.##}x";
        }

        private void Capture_Checked(object sender, RoutedEventArgs e)
        {
            if (_simulatedSource == null) return;

            // Stop playback when entering capture mode
            _isPlaying = false;
            _playPauseButton.Content = "Play";

            _simulatedSource.Pause();
            _simulatedSource.BeginCapture(overwriteExistingCapture: true);
        }

        private void Capture_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_simulatedSource == null) return;

            _simulatedSource.EndCaptureAndSave();

            // Reload the observations for playback
            _simulatedSource.ReloadFromCsv();
        }

        private void Geotrigger_Notification(object? sender, GeotriggerNotificationInfo e)
        {
            Dispatcher.Invoke(() =>
            {
               
                if (e is not FenceGeotriggerNotificationInfo info)
                    return;
                
                // Blue dot enters => show popup Blue dot exits => hide popup
                if (info.FenceNotificationType is FenceNotificationType.Entered)
                {
                    var fence = (ArcGISFeature)info.FenceGeoElement;
                    var layer = (FeatureLayer)fence.FeatureTable!.Layer;

                    
                    _popupViewer.Popup = new Popup(fence, layer.PopupDefinition);
                    _popupPanel.Visibility = Visibility.Visible;

                }
                else
                {
                    _popupPanel.Visibility = Visibility.Collapsed;
                    _popupViewer.Popup = null;
                }
            });
        }
    }
}
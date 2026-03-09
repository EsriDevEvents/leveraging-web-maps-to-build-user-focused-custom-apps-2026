using Esri.ArcGISRuntime.Geometry;
using Esri.ArcGISRuntime.Location;
using Esri.ArcGISRuntime.UI.Controls;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DevSummit2026
{
    public sealed class SimulatedLocationDataSource : LocationDataSource
    {
        public static readonly string CsvPath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "DevSummit2026", "tour_observations.csv");

        private readonly MapView _mapView;

        private readonly List<Observation> _observations = new();
        private int _index;

        private CancellationTokenSource? _pumpCts;
        private volatile bool _paused = true;

        // Capture
        private readonly List<Observation> _captured = new();
        public bool IsCaptureModeEnabled { get; private set; }

        public event EventHandler<int>? CaptureCountChanged;

        public double SpeedMultiplier { get; set; } = 1.0;

        private SimulatedLocationDataSource(MapView mapView)
        {
            _mapView = mapView;
            EnsureCsvFolderExists();
            ReloadFromCsv();
        }

        public static SimulatedLocationDataSource Create(MapView mapView)
            => new SimulatedLocationDataSource(mapView);

        public void ReloadFromCsv()
        {
            _observations.Clear();
            _index = 0;

            if (!File.Exists(CsvPath))
                return;

            _observations.AddRange(ReadCsv(CsvPath));

            if (_observations.Count > 0)
                Publish(_observations[0]);
        }

        public void Play() => _paused = false;
        public void Pause() => _paused = true;

        public void BeginCapture(bool overwriteExistingCapture)
        {
            IsCaptureModeEnabled = true;
            Pause();

            _captured.Clear();
            CaptureCountChanged?.Invoke(this, _captured.Count);

            _mapView.GeoViewTapped += MapView_GeoViewTapped;

            if (overwriteExistingCapture)
            {
                EnsureCsvFolderExists();
                File.WriteAllText(CsvPath, "timestampUtc,latitude,longitude" + Environment.NewLine);
            }
        }

        public void EndCaptureAndSave()
        {
            if (!IsCaptureModeEnabled) return;

            IsCaptureModeEnabled = false;
            _mapView.GeoViewTapped -= MapView_GeoViewTapped;

            EnsureCsvFolderExists();
            WriteCsv(CsvPath, _captured);
        }

        private void MapView_GeoViewTapped(object? sender, GeoViewInputEventArgs e)
        {
            var wgsPoint = (MapPoint)GeometryEngine.Project(e.Location, SpatialReferences.Wgs84);

            var obs = new Observation(
                TimestampUtc: DateTimeOffset.UtcNow,
                Latitude: wgsPoint.Y,
                Longitude: wgsPoint.X);

            _captured.Add(obs);
            CaptureCountChanged?.Invoke(this, _captured.Count);

            // show dot immediately while capturing
            Publish(obs);
        }

        protected override Task OnStartAsync()
        {
            _pumpCts = new CancellationTokenSource();
            _ = Task.Run(() => PumpLoopAsync(_pumpCts.Token));
            return Task.CompletedTask;
        }

        protected override Task OnStopAsync()
        {
            _pumpCts?.Cancel();
            _pumpCts = null;
            _paused = true;
            return Task.CompletedTask;
        }

        private async Task PumpLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (_paused || IsCaptureModeEnabled || _observations.Count < 1)
                {
                    await Task.Delay(50, token);
                    continue;
                }

                var current = _observations[_index];
                Publish(current);

                var nextIndex = (_index + 1) % _observations.Count;
                var next = _observations[nextIndex];

                var dt = next.TimestampUtc - current.TimestampUtc;
                if (dt <= TimeSpan.Zero || dt > TimeSpan.FromSeconds(10))
                    dt = TimeSpan.FromMilliseconds(500);

                var scaledTicks = (long)(dt.Ticks / Math.Max(0.01, SpeedMultiplier));
                var scaled = TimeSpan.FromTicks(Math.Max(TimeSpan.FromMilliseconds(20).Ticks, scaledTicks));

                await Task.Delay(scaled, token);
                _index = nextIndex;
            }
        }

        private void Publish(Observation obs)
        {
            var point = new MapPoint(obs.Longitude, obs.Latitude, SpatialReferences.Wgs84);

            var loc = new Location(
                timestamp: obs.TimestampUtc,
                position: point,
                horizontalAccuracy: 5,     // meters
                verticalAccuracy: 5,       // meters (use a positive number to avoid validation issues)
                velocity: 0,               // m/s
                course: 0,                 // degrees
                isLastKnown: false);

            UpdateLocation(loc);
        }

        private static IEnumerable<Observation> ReadCsv(string path)
        {
            var lines = File.ReadAllLines(path);

            foreach (var raw in lines.Skip(1))
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;

                var parts = raw.Split(',');
                if (parts.Length < 3) continue;

                var ts = DateTimeOffset.Parse(parts[0], CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
                var lat = double.Parse(parts[1], CultureInfo.InvariantCulture);
                var lon = double.Parse(parts[2], CultureInfo.InvariantCulture);

                yield return new Observation(ts, lat, lon);
            }
        }

        private static void WriteCsv(string path, IReadOnlyList<Observation> observations)
        {
            using var sw = new StreamWriter(path, false);
            sw.WriteLine("timestampUtc,latitude,longitude");

            foreach (var o in observations)
            {
                sw.WriteLine(string.Join(",",
                    o.TimestampUtc.ToString("O", CultureInfo.InvariantCulture),
                    o.Latitude.ToString(CultureInfo.InvariantCulture),
                    o.Longitude.ToString(CultureInfo.InvariantCulture)));
            }
        }

        private static void EnsureCsvFolderExists()
        {
            var folder = Path.GetDirectoryName(CsvPath)!;
            Directory.CreateDirectory(folder);
        }

        private sealed record Observation(DateTimeOffset TimestampUtc, double Latitude, double Longitude);
    }
}
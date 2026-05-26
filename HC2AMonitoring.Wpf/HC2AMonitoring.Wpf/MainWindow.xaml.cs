using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CSharpBasics.Application.Services;
using CSharpBasics.Domain.Models;

namespace HC2AMonitoring.Wpf;

public partial class MainWindow : Window
{
    private const string SensorPortName = "COM4";
    private const int MaxChartPoints = 60;
    private static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(1);

    private readonly HC2A_AlarmService _alarmService = new();
    private readonly List<HC2A_Reading> _chartReadings = new();
    private CancellationTokenSource? _samplingCancellation;
    private HC2A_SensorService? _sensorService;
    private bool _isSampling;

    public MainWindow()
    {
        InitializeComponent();
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isSampling)
        {
            return;
        }

        if (!TryCreateThreshold(out var threshold))
        {
            return;
        }

        _isSampling = true;
        _chartReadings.Clear();
        RedrawChart();

        StartButton.IsEnabled = false;
        StopButton.IsEnabled = true;
        ConnectionStatusTextBlock.Text = "Connecting";
        ConnectionStatusTextBlock.Foreground = Brushes.LightGoldenrodYellow;
        AlarmTextBlock.Text = "Connecting sensor";
        AlarmTextBlock.Foreground = Brushes.LightGoldenrodYellow;

        _samplingCancellation = new CancellationTokenSource();
        _sensorService = new HC2A_SensorService(SensorPortName);

        try
        {
            _sensorService.Open();
            ConnectionStatusTextBlock.Text = "Connected";
            ConnectionStatusTextBlock.Foreground = Brushes.LightGreen;

            var nextRunAt = DateTimeOffset.UtcNow;

            while (!_samplingCancellation.Token.IsCancellationRequested)
            {
                nextRunAt += SampleInterval;

                var reading = await _sensorService.ReadAsync();
                var alarmResult = _alarmService.Check(reading, threshold);

                UpdateReading(reading, alarmResult);

                var delay = nextRunAt - DateTimeOffset.UtcNow;
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, _samplingCancellation.Token);
                }
            }
        }
        catch (OperationCanceledException)
        {
            AlarmTextBlock.Text = "Sampling stopped";
            AlarmTextBlock.Foreground = Brushes.LightGoldenrodYellow;
        }
        catch (Exception ex)
        {
            ConnectionStatusTextBlock.Text = "Error";
            ConnectionStatusTextBlock.Foreground = Brushes.IndianRed;
            AlarmTextBlock.Text = ex.Message;
            AlarmTextBlock.Foreground = Brushes.IndianRed;
            LastAlarmTextBlock.Text = ex.Message;
            LastAlarmTextBlock.Foreground = Brushes.IndianRed;
        }
        finally
        {
            _sensorService?.Dispose();
            _sensorService = null;
            _samplingCancellation?.Dispose();
            _samplingCancellation = null;

            _isSampling = false;
            StartButton.IsEnabled = true;
            StopButton.IsEnabled = false;

            if (ConnectionStatusTextBlock.Text != "Error")
            {
                ConnectionStatusTextBlock.Text = "Idle";
                ConnectionStatusTextBlock.Foreground = Brushes.LightGray;
            }
        }
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        _samplingCancellation?.Cancel();
        StopButton.IsEnabled = false;
        ConnectionStatusTextBlock.Text = "Stopping";
        ConnectionStatusTextBlock.Foreground = Brushes.LightGoldenrodYellow;
    }

    private bool TryCreateThreshold(out HC2A_Threshold threshold)
    {
        threshold = new HC2A_Threshold(0, 0, 0, 0, 0, 0);

        if (!TryReadDouble(HumidityMinTextBox.Text, out var humidityMin) ||
            !TryReadDouble(HumidityMaxTextBox.Text, out var humidityMax) ||
            !TryReadDouble(TemperatureMinTextBox.Text, out var temperatureMin) ||
            !TryReadDouble(TemperatureMaxTextBox.Text, out var temperatureMax))
        {
            AlarmTextBlock.Text = "Check threshold numbers.";
            AlarmTextBlock.Foreground = Brushes.IndianRed;
            return false;
        }

        if (humidityMin > humidityMax || temperatureMin > temperatureMax)
        {
            AlarmTextBlock.Text = "Min value cannot be greater than max value.";
            AlarmTextBlock.Foreground = Brushes.IndianRed;
            return false;
        }

        threshold = new HC2A_Threshold(
            humidityMin,
            humidityMax,
            temperatureMin,
            temperatureMax,
            0,
            0);

        return true;
    }

    private static bool TryReadDouble(string text, out double value)
    {
        return double.TryParse(
            text.Trim(),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out value);
    }

    private void UpdateReading(HC2A_Reading reading, HC2A_AlarmResult alarmResult)
    {
        var temperatureText = reading.Temperature.ToString("F1", CultureInfo.InvariantCulture);
        var humidityText = reading.Humidity.ToString("F1", CultureInfo.InvariantCulture);

        CurrentTemperatureTextBlock.Text = temperatureText;
        CurrentHumidityTextBlock.Text = humidityText;
        FeedTemperatureTextBlock.Text = temperatureText;
        FeedHumidityTextBlock.Text = humidityText;
        RawResponseTextBox.Text = reading.RawResponse;
        AddChartReading(reading);

        if (alarmResult.HasAlarm)
        {
            AlarmTextBlock.Text = alarmResult.Message;
            AlarmTextBlock.Foreground = Brushes.LightSalmon;
            LastAlarmTextBlock.Text = alarmResult.Message;
            LastAlarmTextBlock.Foreground = Brushes.LightSalmon;
            FeedStatusTextBlock.Text = "Alarm";
            FeedStatusTextBlock.Foreground = Brushes.LightSalmon;
        }
        else
        {
            AlarmTextBlock.Text = "No alarm";
            AlarmTextBlock.Foreground = Brushes.LightGreen;
            LastAlarmTextBlock.Text = "No alarm";
            LastAlarmTextBlock.Foreground = Brushes.LightGray;
            FeedStatusTextBlock.Text = "Normal";
            FeedStatusTextBlock.Foreground = Brushes.LightGreen;
        }
    }

    private void AddChartReading(HC2A_Reading reading)
    {
        _chartReadings.Add(reading);

        if (_chartReadings.Count > MaxChartPoints)
        {
            _chartReadings.RemoveAt(0);
        }

        RedrawChart();
    }

    private void ChartCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        RedrawChart();
    }

    private void RedrawChart()
    {
        if (ChartCanvas.ActualWidth <= 0 || ChartCanvas.ActualHeight <= 0)
        {
            return;
        }

        if (_chartReadings.Count == 0)
        {
            TemperaturePolyline.Points = new PointCollection();
            HumidityPolyline.Points = new PointCollection();
            ChartMaxLabelTextBlock.Text = "--";
            ChartMinLabelTextBlock.Text = "--";
            ChartLatestTimeTextBlock.Text = "waiting";
            return;
        }

        var minValue = _chartReadings.Min(reading => Math.Min(reading.Temperature, reading.Humidity));
        var maxValue = _chartReadings.Max(reading => Math.Max(reading.Temperature, reading.Humidity));

        if (Math.Abs(maxValue - minValue) < 0.1)
        {
            minValue -= 1;
            maxValue += 1;
        }

        var temperaturePoints = new PointCollection();
        var humidityPoints = new PointCollection();

        for (var i = 0; i < _chartReadings.Count; i++)
        {
            var x = GetChartX(i, _chartReadings.Count);
            temperaturePoints.Add(new Point(x, GetChartY(_chartReadings[i].Temperature, minValue, maxValue)));
            humidityPoints.Add(new Point(x, GetChartY(_chartReadings[i].Humidity, minValue, maxValue)));
        }

        if (_chartReadings.Count == 1)
        {
            var reading = _chartReadings[0];
            temperaturePoints.Add(new Point(ChartCanvas.ActualWidth, GetChartY(reading.Temperature, minValue, maxValue)));
            humidityPoints.Add(new Point(ChartCanvas.ActualWidth, GetChartY(reading.Humidity, minValue, maxValue)));
        }

        TemperaturePolyline.Points = temperaturePoints;
        HumidityPolyline.Points = humidityPoints;
        ChartMaxLabelTextBlock.Text = maxValue.ToString("F1", CultureInfo.InvariantCulture);
        ChartMinLabelTextBlock.Text = minValue.ToString("F1", CultureInfo.InvariantCulture);
        ChartLatestTimeTextBlock.Text = _chartReadings[^1].Timestamp.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
    }

    private void ChartCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_chartReadings.Count == 0 || ChartCanvas.ActualWidth <= 0 || ChartCanvas.ActualHeight <= 0)
        {
            HideChartHover();
            return;
        }

        var mousePosition = e.GetPosition(ChartCanvas);
        var index = GetNearestChartIndex(mousePosition.X);
        var reading = _chartReadings[index];

        var minValue = _chartReadings.Min(item => Math.Min(item.Temperature, item.Humidity));
        var maxValue = _chartReadings.Max(item => Math.Max(item.Temperature, item.Humidity));

        if (Math.Abs(maxValue - minValue) < 0.1)
        {
            minValue -= 1;
            maxValue += 1;
        }

        var x = GetChartX(index, _chartReadings.Count);
        var temperatureY = GetChartY(reading.Temperature, minValue, maxValue);
        var humidityY = GetChartY(reading.Humidity, minValue, maxValue);

        ChartHoverLine.X1 = x;
        ChartHoverLine.X2 = x;
        ChartHoverLine.Y2 = ChartCanvas.ActualHeight;
        ChartHoverLine.Visibility = Visibility.Visible;

        MoveMarker(TemperatureHoverMarker, x, temperatureY);
        MoveMarker(HumidityHoverMarker, x, humidityY);

        ChartTooltipTextBlock.Text =
            $"{reading.Timestamp:HH:mm:ss}\n" +
            $"온도 {reading.Temperature.ToString("F1", CultureInfo.InvariantCulture)} °C\n" +
            $"습도 {reading.Humidity.ToString("F1", CultureInfo.InvariantCulture)} %RH";

        ChartTooltipBorder.Visibility = Visibility.Visible;
        ChartTooltipBorder.UpdateLayout();

        var tooltipLeft = x + 12;
        var tooltipTop = Math.Min(temperatureY, humidityY) - 12;

        if (tooltipLeft + ChartTooltipBorder.ActualWidth > ChartCanvas.ActualWidth)
        {
            tooltipLeft = x - ChartTooltipBorder.ActualWidth - 12;
        }

        tooltipTop = Math.Clamp(
            tooltipTop,
            0,
            Math.Max(0, ChartCanvas.ActualHeight - ChartTooltipBorder.ActualHeight));

        Canvas.SetLeft(ChartTooltipBorder, tooltipLeft);
        Canvas.SetTop(ChartTooltipBorder, tooltipTop);
    }

    private void ChartCanvas_MouseLeave(object sender, MouseEventArgs e)
    {
        HideChartHover();
    }

    private int GetNearestChartIndex(double x)
    {
        if (_chartReadings.Count <= 1 || ChartCanvas.ActualWidth <= 0)
        {
            return 0;
        }

        var ratio = Math.Clamp(x / ChartCanvas.ActualWidth, 0, 1);
        return (int)Math.Round(ratio * (_chartReadings.Count - 1));
    }

    private static void MoveMarker(UIElement marker, double x, double y)
    {
        const double markerSize = 10;

        Canvas.SetLeft(marker, x - markerSize / 2);
        Canvas.SetTop(marker, y - markerSize / 2);
        marker.Visibility = Visibility.Visible;
    }

    private void HideChartHover()
    {
        ChartHoverLine.Visibility = Visibility.Collapsed;
        TemperatureHoverMarker.Visibility = Visibility.Collapsed;
        HumidityHoverMarker.Visibility = Visibility.Collapsed;
        ChartTooltipBorder.Visibility = Visibility.Collapsed;
    }

    private double GetChartX(int index, int count)
    {
        if (count <= 1)
        {
            return 0;
        }

        return ChartCanvas.ActualWidth / (count - 1) * index;
    }

    private double GetChartY(double value, double minValue, double maxValue)
    {
        var ratio = (value - minValue) / (maxValue - minValue);
        ratio = Math.Clamp(ratio, 0, 1);

        return ChartCanvas.ActualHeight - (ratio * ChartCanvas.ActualHeight);
    }
}

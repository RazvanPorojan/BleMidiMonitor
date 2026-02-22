using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;

namespace BleMidiMonitor
{
    public sealed partial class FretboardWindow : Window
    {
        private readonly FretState _fretState;
        private readonly Dictionary<string, Rectangle> _fretRectangles = new Dictionary<string, Rectangle>();
        private readonly Dictionary<string, TextBlock> _fretLabels = new Dictionary<string, TextBlock>();
        private const int FretCount = 22;
        private const int StringCount = 6;
        private const double FretWidth = 45;
        private const double StringHeight = 35;
        private const double StartX = 80;
        private const double StartY = 10;
        private readonly string[] StringNames = { "e", "B", "G", "D", "A", "E" }; // High to low

        private readonly SolidColorBrush _normalBrush;
        private readonly SolidColorBrush _activeBrush;
        private readonly SolidColorBrush _fretLineBrush;
        private readonly SolidColorBrush _stringLineBrush;

        public FretboardWindow(FretState fretState)
        {
            InitializeComponent();
            _fretState = fretState;
            _fretState.FretChanged += OnFretChanged;

            // Initialize colors
            _normalBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(40, 128, 128, 128));
            _activeBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 200, 100));
            _fretLineBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(180, 160, 160, 160));
            _stringLineBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(150, 140, 140, 140));

            // Set window to always on top
            SetAlwaysOnTop();

            // Set initial window size
            AppWindow.Resize(new Windows.Graphics.SizeInt32 { Width = 1150, Height = 350 });

            DrawFretboard();
        }

        private void SetAlwaysOnTop()
        {
            // Get the window handle for the current window
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

            // Set always on top presenter
            var presenter = appWindow.Presenter as Microsoft.UI.Windowing.OverlappedPresenter;
            if (presenter != null)
            {
                presenter.IsAlwaysOnTop = true;
            }
        }

        private void DrawFretboard()
        {
            FretboardCanvas.Children.Clear();
            _fretRectangles.Clear();
            _fretLabels.Clear();

            // Draw string labels on the left
            for (int str = 0; str < StringCount; str++)
            {
                var label = new TextBlock
                {
                    Text = StringNames[str],
                    FontSize = 16,
                    FontWeight = new Windows.UI.Text.FontWeight(700),
                    Foreground = new SolidColorBrush(Colors.White),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Canvas.SetLeft(label, StartX - 40);
                Canvas.SetTop(label, StartY + str * StringHeight + 8);
                FretboardCanvas.Children.Add(label);
            }

            // Draw fret numbers at the top
            for (int fret = 0; fret < FretCount; fret++)
            {
                var fretLabel = new TextBlock
                {
                    Text = (fret + 1).ToString(),
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Colors.LightGray),
                    TextAlignment = TextAlignment.Center,
                    Width = FretWidth
                };
                Canvas.SetLeft(fretLabel, StartX + fret * FretWidth);
                Canvas.SetTop(fretLabel, StartY - 20);
                FretboardCanvas.Children.Add(fretLabel);
            }

            // Draw fret positions
            for (int str = 0; str < StringCount; str++)
            {
                for (int fret = 0; fret < FretCount; fret++)
                {
                    double x = StartX + fret * FretWidth;
                    double y = StartY + str * StringHeight;

                    // Draw fret cell background
                    var rect = new Rectangle
                    {
                        Width = FretWidth - 2,
                        Height = StringHeight - 2,
                        Fill = _normalBrush,
                        Stroke = _fretLineBrush,
                        StrokeThickness = 1,
                        RadiusX = 3,
                        RadiusY = 3
                    };

                    Canvas.SetLeft(rect, x + 1);
                    Canvas.SetTop(rect, y + 1);
                    FretboardCanvas.Children.Add(rect);

                    // Create label for fret number (initially hidden)
                    var label = new TextBlock
                    {
                        Text = (fret + 1).ToString(),
                        FontSize = 18,
                        FontWeight = new Windows.UI.Text.FontWeight(700),
                        Foreground = new SolidColorBrush(Colors.White),
                        TextAlignment = TextAlignment.Center,
                        Width = FretWidth - 2,
                        Visibility = Visibility.Collapsed
                    };

                    Canvas.SetLeft(label, x + 1);
                    Canvas.SetTop(label, y + 6);
                    FretboardCanvas.Children.Add(label);

                    string key = $"{str + 1}_{fret + 1}";
                    _fretRectangles[key] = rect;
                    _fretLabels[key] = label;
                }

                // Draw horizontal string line
                var stringLine = new Line
                {
                    X1 = StartX,
                    Y1 = StartY + str * StringHeight + StringHeight / 2,
                    X2 = StartX + FretCount * FretWidth,
                    Y2 = StartY + str * StringHeight + StringHeight / 2,
                    Stroke = _stringLineBrush,
                    StrokeThickness = 1.5
                };
                FretboardCanvas.Children.Add(stringLine);
            }

            // Draw vertical fret lines
            for (int fret = 0; fret <= FretCount; fret++)
            {
                var fretLine = new Line
                {
                    X1 = StartX + fret * FretWidth,
                    Y1 = StartY,
                    X2 = StartX + fret * FretWidth,
                    Y2 = StartY + StringCount * StringHeight,
                    Stroke = _fretLineBrush,
                    StrokeThickness = fret == 0 ? 3 : 1.5
                };
                FretboardCanvas.Children.Add(fretLine);
            }
        }

        private void OnFretChanged(object sender, FretChangedEventArgs e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                UpdateFretDisplay(e.StringNumber, e.FretNumber, e.IsActive);
            });
        }

        private void UpdateFretDisplay(int stringNumber, int fretNumber, bool isActive)
        {
            if (stringNumber < 1 || stringNumber > StringCount || fretNumber < 1 || fretNumber > FretCount)
                return;

            string key = $"{stringNumber}_{fretNumber}";

            if (_fretRectangles.TryGetValue(key, out var rect) && _fretLabels.TryGetValue(key, out var label))
            {
                if (isActive)
                {
                    rect.Fill = _activeBrush;
                    label.Visibility = Visibility.Visible;
                }
                else
                {
                    rect.Fill = _normalBrush;
                    label.Visibility = Visibility.Collapsed;
                }
            }
        }

        public void Cleanup()
        {
            _fretState.FretChanged -= OnFretChanged;
        }
    }
}

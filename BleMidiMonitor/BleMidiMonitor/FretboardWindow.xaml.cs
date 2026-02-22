using Microsoft.UI;
using Microsoft.UI.Dispatching;
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
        private const double StringHeight = 35;
        private const double StartX = 80;
        private const double StartY = 10;
        private readonly string[] StringNames = { "e", "B", "G", "D", "A", "E" }; // High to low

        // Realistic fret spacing configuration
        private const double ScaleLength = 650.0;      // Virtual scale length in pixels
        private const double FretboardScale = 0.95;    // Scale factor to fit window
        private readonly double[] _fretPositions;      // Cumulative X positions [0..22]
        private readonly double[] _fretWidths;         // Individual fret widths [0..21]

        // Open string notes for standard tuning (chromatic scale index)
        // E=4, A=9, D=2, G=7, B=11, e=4
        private readonly int[] OpenStringNotes = { 4, 11, 7, 2, 9, 4 }; // High to low: e, B, G, D, A, E
        private readonly string[] NoteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

        // String thicknesses (high e to low E) - thinner for higher strings, thicker for lower
        private readonly double[] StringThicknesses = { 1.0, 1.5, 2.0, 2.5, 3.0, 3.5 };

        private readonly SolidColorBrush _normalBrush;
        private readonly SolidColorBrush _activeBrush;
        private readonly SolidColorBrush _fretLineBrush;
        private readonly SolidColorBrush _stringLineBrush;

        // Performance optimization caches
        private readonly Dictionary<(int, int), bool> _fretStateCache = new Dictionary<(int, int), bool>();
        private readonly Dictionary<(int, int), string> _keyCache = new Dictionary<(int, int), string>();

        // Chord detection
        private readonly ChordDetector _chordDetector;
        private DateTime _lastChordUpdate = DateTime.MinValue;
        private const int ChordUpdateThrottleMs = 100;

        public FretboardWindow(FretState fretState)
        {
            InitializeComponent();
            _fretState = fretState;
            _fretState.FretChanged += OnFretChanged;

            // Initialize chord detector
            _chordDetector = new ChordDetector();

            // Initialize colors
            _normalBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(40, 128, 128, 128));
            _activeBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 200, 100));
            _fretLineBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(180, 160, 160, 160));
            _stringLineBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(200, 180, 180, 180));

            // Pre-compute dictionary keys to avoid string allocations
            for (int str = 1; str <= StringCount; str++)
            {
                for (int fret = 1; fret <= FretCount; fret++)
                {
                    _keyCache[(str, fret)] = $"{str}_{fret}";
                }
            }

            // Pre-calculate realistic fret positions (must be done before window sizing)
            _fretPositions = CalculateFretPositions();
            _fretWidths = CalculateFretWidths(_fretPositions);

            // Set window to always on top
            SetAlwaysOnTop();

            // Set initial window size based on calculated fretboard width
            int totalWidth = (int)Math.Ceiling(_fretPositions[FretCount] + 50); // 50px end margin
            AppWindow.Resize(new Windows.Graphics.SizeInt32 { Width = totalWidth, Height = 350 });

            DrawFretboard();
        }

        private double[] CalculateFretPositions()
        {
            var positions = new double[FretCount + 1]; // 0 to 22 inclusive
            positions[0] = StartX; // Nut position

            for (int fret = 1; fret <= FretCount; fret++)
            {
                // Physics formula: distance from nut = ScaleLength × (1 - 2^(-fret/12))
                double distanceFromNut = ScaleLength * (1.0 - Math.Pow(2.0, -fret / 12.0));
                positions[fret] = StartX + (distanceFromNut * FretboardScale);
            }

            return positions;
        }

        private double[] CalculateFretWidths(double[] positions)
        {
            var widths = new double[FretCount];
            for (int fret = 0; fret < FretCount; fret++)
            {
                widths[fret] = positions[fret + 1] - positions[fret];
            }
            return widths;
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

        private string GetNoteName(int stringNumber, int fretNumber)
        {
            // stringNumber is 1-based, convert to 0-based for array access
            int stringIndex = stringNumber - 1;
            if (stringIndex < 0 || stringIndex >= OpenStringNotes.Length)
                return "";

            // Calculate note: (open string note + fret number) mod 12
            int noteIndex = (OpenStringNotes[stringIndex] + fretNumber) % 12;
            return NoteNames[noteIndex];
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
                    Width = _fretWidths[fret]
                };
                Canvas.SetLeft(fretLabel, _fretPositions[fret]);
                Canvas.SetTop(fretLabel, StartY - 20);
                FretboardCanvas.Children.Add(fretLabel);
            }

            // Draw fret positions
            for (int str = 0; str < StringCount; str++)
            {
                for (int fret = 0; fret < FretCount; fret++)
                {
                    double x = _fretPositions[fret];
                    double y = StartY + str * StringHeight;

                    // Draw fret cell background
                    var rect = new Rectangle
                    {
                        Width = _fretWidths[fret] - 2,
                        Height = StringHeight - 2,
                        Fill = _normalBrush,
                        RadiusX = 3,
                        RadiusY = 3
                    };

                    Canvas.SetLeft(rect, x + 1);
                    Canvas.SetTop(rect, y + 1);
                    FretboardCanvas.Children.Add(rect);

                    // Create label for fret number and note (initially hidden)
                    string noteName = GetNoteName(str + 1, fret + 1);
                    var label = new TextBlock
                    {
                        Text = $"{fret + 1} {noteName}",
                        FontSize = 16,
                        FontWeight = new Windows.UI.Text.FontWeight(700),
                        Foreground = new SolidColorBrush(Colors.White),
                        TextAlignment = TextAlignment.Center,
                        Width = _fretWidths[fret] - 2,
                        Visibility = Visibility.Collapsed
                    };

                    Canvas.SetLeft(label, x + 1);
                    Canvas.SetTop(label, y + 6);
                    FretboardCanvas.Children.Add(label);

                    string key = $"{str + 1}_{fret + 1}";
                    _fretRectangles[key] = rect;
                    _fretLabels[key] = label;
                }

                // Draw horizontal string line with varying thickness
                var stringLine = new Line
                {
                    X1 = StartX,
                    Y1 = StartY + str * StringHeight + StringHeight / 2,
                    X2 = _fretPositions[FretCount],
                    Y2 = StartY + str * StringHeight + StringHeight / 2,
                    Stroke = _stringLineBrush,
                    StrokeThickness = StringThicknesses[str]
                };
                FretboardCanvas.Children.Add(stringLine);
            }

            // Draw vertical fret lines
            for (int fret = 0; fret <= FretCount; fret++)
            {
                var fretLine = new Line
                {
                    X1 = _fretPositions[fret],
                    Y1 = StartY,
                    X2 = _fretPositions[fret],
                    Y2 = StartY + StringCount * StringHeight,
                    Stroke = _fretLineBrush,
                    StrokeThickness = fret == 0 ? 3 : 1.5
                };
                FretboardCanvas.Children.Add(fretLine);
            }

            // Draw fret markers (inlay dots)
            var dotBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(100, 200, 200, 200));
            int[] singleDotFrets = { 3, 5, 7, 9, 15, 17, 19, 21 };
            int[] doubleDotFrets = { 12 };

            foreach (int fret in singleDotFrets)
            {
                if (fret <= FretCount)
                {
                    var dot = new Ellipse
                    {
                        Width = 12,
                        Height = 12,
                        Fill = dotBrush
                    };
                    double fretCenterX = (_fretPositions[fret - 1] + _fretPositions[fret]) / 2.0;
                    double x = fretCenterX - 6; // Center 12px dot
                    double y = StartY + (StringCount * StringHeight) / 2 - 6;
                    Canvas.SetLeft(dot, x);
                    Canvas.SetTop(dot, y);
                    FretboardCanvas.Children.Add(dot);
                }
            }

            foreach (int fret in doubleDotFrets)
            {
                if (fret <= FretCount)
                {
                    double fretCenterX = (_fretPositions[fret - 1] + _fretPositions[fret]) / 2.0;
                    double x = fretCenterX - 6; // Center 12px dot

                    // Top dot
                    var dot1 = new Ellipse
                    {
                        Width = 12,
                        Height = 12,
                        Fill = dotBrush
                    };
                    double y1 = StartY + StringHeight * 1.5 - 6;
                    Canvas.SetLeft(dot1, x);
                    Canvas.SetTop(dot1, y1);
                    FretboardCanvas.Children.Add(dot1);

                    // Bottom dot
                    var dot2 = new Ellipse
                    {
                        Width = 12,
                        Height = 12,
                        Fill = dotBrush
                    };
                    double y2 = StartY + StringHeight * 4.5 - 6;
                    Canvas.SetLeft(dot2, x);
                    Canvas.SetTop(dot2, y2);
                    FretboardCanvas.Children.Add(dot2);
                }
            }
        }

        private void OnFretChanged(object sender, FretChangedEventArgs e)
        {
            // Update immediately with High priority for low latency
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.High, () =>
            {
                UpdateFretDisplay(e.StringNumber, e.FretNumber, e.IsActive);

                // Throttle chord detection to avoid excessive updates during rapid changes
                var now = DateTime.Now;
                if ((now - _lastChordUpdate).TotalMilliseconds >= ChordUpdateThrottleMs)
                {
                    DetectAndUpdateChord();
                    _lastChordUpdate = now;
                }
            });
        }

        private void UpdateFretDisplay(int stringNumber, int fretNumber, bool isActive)
        {
            if (stringNumber < 1 || stringNumber > StringCount || fretNumber < 1 || fretNumber > FretCount)
                return;

            var key = (stringNumber, fretNumber);

            // Check cache - skip if no change
            if (_fretStateCache.TryGetValue(key, out var cached) && cached == isActive)
                return;

            _fretStateCache[key] = isActive;

            // Get pre-computed string key
            string dictKey = _keyCache[key];

            if (_fretRectangles.TryGetValue(dictKey, out var rect) && _fretLabels.TryGetValue(dictKey, out var label))
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

        private void DetectAndUpdateChord()
        {
            var activeFrets = _fretState.GetAllActiveFrets();
            var chordResult = _chordDetector.DetectChord(activeFrets);
            UpdateChordDisplay(chordResult);
        }

        private void UpdateChordDisplay(ChordResult result)
        {
            if (result.IsValid && result.Confidence >= 60)
            {
                // Valid chord detected
                ChordNameDisplay.Text = result.ChordName;
                ChordNameDisplay.Foreground = new SolidColorBrush(
                    Windows.UI.Color.FromArgb(255, 0, 200, 100)); // Green
                ChordNotesDisplay.Text = $"({string.Join(", ", result.NoteNames)})";
            }
            else if (result.NoteNames?.Count > 0)
            {
                // Notes detected but no clear chord match
                ChordNameDisplay.Text = "?";
                ChordNameDisplay.Foreground = new SolidColorBrush(
                    Windows.UI.Color.FromArgb(255, 200, 200, 0)); // Yellow
                ChordNotesDisplay.Text = $"({string.Join(", ", result.NoteNames)})";
            }
            else
            {
                // No notes pressed
                ChordNameDisplay.Text = "--";
                ChordNameDisplay.Foreground = new SolidColorBrush(Colors.Gray);
                ChordNotesDisplay.Text = "";
            }
        }

        public void Cleanup()
        {
            _fretState.FretChanged -= OnFretChanged;
        }
    }
}

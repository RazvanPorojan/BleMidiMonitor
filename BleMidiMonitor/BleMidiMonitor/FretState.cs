using System.Collections.Generic;
using System.Linq;

namespace BleMidiMonitor
{
    public class FretState
    {
        private readonly Dictionary<int, HashSet<int>> _activeFretsPerString = new Dictionary<int, HashSet<int>>();
        private readonly object _lock = new object();

        public event System.EventHandler<FretChangedEventArgs> FretChanged;

        public void UpdateFret(int stringNumber, int fretNumber, bool isActive)
        {
            bool shouldFire = false;

            lock (_lock)
            {
                if (isActive)
                {
                    // Get or create the set for this string
                    if (!_activeFretsPerString.TryGetValue(stringNumber, out var frets))
                    {
                        frets = new HashSet<int>();
                        _activeFretsPerString[stringNumber] = frets;
                    }

                    // Add fret to the set - returns true if newly added
                    shouldFire = frets.Add(fretNumber);
                }
                else
                {
                    // Remove fret from the set only if it's actually tracked
                    if (_activeFretsPerString.TryGetValue(stringNumber, out var frets))
                    {
                        shouldFire = frets.Remove(fretNumber);

                        // Clean up empty set
                        if (frets.Count == 0)
                        {
                            _activeFretsPerString.Remove(stringNumber);
                        }
                    }
                }
            }

            // Fire event outside lock
            if (shouldFire)
            {
                FretChanged?.Invoke(this, new FretChangedEventArgs
                {
                    StringNumber = stringNumber,
                    FretNumber = fretNumber,
                    IsActive = isActive
                });
            }
        }

        public int? GetActiveFret(int stringNumber)
        {
            lock (_lock)
            {
                if (_activeFretsPerString.TryGetValue(stringNumber, out var frets) && frets.Count > 0)
                {
                    // Return the highest fret (most realistic for guitar)
                    return frets.Max();
                }
                return null;
            }
        }

        public Dictionary<int, int> GetAllActiveFrets()
        {
            lock (_lock)
            {
                var result = new Dictionary<int, int>();
                foreach (var kvp in _activeFretsPerString)
                {
                    if (kvp.Value.Count > 0)
                    {
                        result[kvp.Key] = kvp.Value.Max();
                    }
                }
                return result;
            }
        }

        public Dictionary<int, HashSet<int>> GetAllActiveFretsFull()
        {
            lock (_lock)
            {
                var result = new Dictionary<int, HashSet<int>>();
                foreach (var kvp in _activeFretsPerString)
                {
                    result[kvp.Key] = new HashSet<int>(kvp.Value);
                }
                return result;
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _activeFretsPerString.Clear();
            }
        }
    }

    public class FretChangedEventArgs : System.EventArgs
    {
        public int StringNumber { get; set; }
        public int FretNumber { get; set; }
        public bool IsActive { get; set; }
    }
}

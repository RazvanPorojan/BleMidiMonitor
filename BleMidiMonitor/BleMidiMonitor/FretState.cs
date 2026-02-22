using System.Collections.Generic;

namespace BleMidiMonitor
{
    public class FretState
    {
        private readonly Dictionary<int, int> _activeFretsPerString = new Dictionary<int, int>();
        private readonly object _lock = new object();

        public event System.EventHandler<FretChangedEventArgs> FretChanged;

        public void UpdateFret(int stringNumber, int fretNumber, bool isActive)
        {
            bool shouldFire = false;

            lock (_lock)
            {
                if (isActive)
                {
                    // Skip if already active at same fret
                    if (_activeFretsPerString.TryGetValue(stringNumber, out int current) && current == fretNumber)
                        return;

                    _activeFretsPerString[stringNumber] = fretNumber;
                    shouldFire = true;
                }
                else
                {
                    // Skip if not currently active
                    shouldFire = _activeFretsPerString.Remove(stringNumber);
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
                return _activeFretsPerString.TryGetValue(stringNumber, out int fret) ? fret : null;
            }
        }

        public Dictionary<int, int> GetAllActiveFrets()
        {
            lock (_lock)
            {
                return new Dictionary<int, int>(_activeFretsPerString);
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

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
            lock (_lock)
            {
                if (isActive)
                {
                    _activeFretsPerString[stringNumber] = fretNumber;
                }
                else
                {
                    _activeFretsPerString.Remove(stringNumber);
                }
            }

            FretChanged?.Invoke(this, new FretChangedEventArgs
            {
                StringNumber = stringNumber,
                FretNumber = fretNumber,
                IsActive = isActive
            });
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

using Microsoft.UI.Dispatching;
using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;

namespace BleMidiMonitor
{
    public class MidiLogBatcher : IDisposable
    {
        private readonly ConcurrentQueue<string> _messageQueue;
        private readonly ObservableCollection<string> _displayCollection;
        private readonly DispatcherQueue _dispatcherQueue;
        private readonly DispatcherQueueTimer _batchTimer;
        private readonly int _maxMessages;
        private readonly object _lock = new object();
        private bool _disposed;

        public ObservableCollection<string> DisplayCollection => _displayCollection;
        public int MessageCount { get; private set; }

        public MidiLogBatcher(int maxMessages, DispatcherQueue dispatcherQueue)
        {
            _maxMessages = maxMessages;
            _dispatcherQueue = dispatcherQueue;
            _messageQueue = new ConcurrentQueue<string>();
            _displayCollection = new ObservableCollection<string>();

            // Create timer to flush messages every 50ms (20 Hz)
            _batchTimer = _dispatcherQueue.CreateTimer();
            _batchTimer.Interval = TimeSpan.FromMilliseconds(50);
            _batchTimer.Tick += OnBatchTimerTick;
            _batchTimer.Start();
        }

        public void AddMessage(string message)
        {
            if (_disposed) return;
            _messageQueue.Enqueue(message);
            MessageCount++;
        }

        public void Clear()
        {
            lock (_lock)
            {
                // Clear the queue
                while (_messageQueue.TryDequeue(out _)) { }

                // Clear the display collection on UI thread
                _dispatcherQueue.TryEnqueue(() =>
                {
                    _displayCollection.Clear();
                });

                MessageCount = 0;
            }
        }

        private void OnBatchTimerTick(DispatcherQueueTimer sender, object args)
        {
            if (_disposed || _messageQueue.IsEmpty) return;

            lock (_lock)
            {
                // Collect all pending messages
                var batch = new System.Collections.Generic.List<string>();
                while (_messageQueue.TryDequeue(out var message))
                {
                    batch.Add(message);
                }

                if (batch.Count == 0) return;

                // Update UI on dispatcher thread
                _dispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        // Check if user is scrolled to bottom before update
                        bool shouldAutoScroll = _displayCollection.Count == 0 ||
                                               _displayCollection.Count < _maxMessages;

                        // Add all batched messages
                        foreach (var message in batch)
                        {
                            _displayCollection.Add(message);
                        }

                        // Prune old messages if we exceed max
                        while (_displayCollection.Count > _maxMessages)
                        {
                            _displayCollection.RemoveAt(0);
                        }
                    }
                    catch (Exception)
                    {
                        // Ignore errors during UI update
                    }
                });
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _batchTimer?.Stop();

            while (_messageQueue.TryDequeue(out _)) { }
        }
    }
}

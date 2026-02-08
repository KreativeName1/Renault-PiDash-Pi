namespace PiDash.Core;

public enum BoundedQueueFullMode
{
    DropNewest,
    DropOldest,
    OverwriteOldest
}

public sealed class BoundedQueue<T>
{
    private readonly T[] _buffer;
    private readonly object _lock = new();
    private int _head;  // next read
    private int _tail;  // next write
    private int _count;

    public int Capacity { get; }
    public int Count { get { lock (_lock) return _count; } }

    public BoundedQueue(int capacity)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        Capacity = capacity;
        _buffer = new T[capacity];
    }

    public bool TryEnqueue(T item, BoundedQueueFullMode fullMode = BoundedQueueFullMode.DropNewest)
    {
        lock (_lock)
        {
            if (_count == Capacity)
            {
                switch (fullMode)
                {
                    case BoundedQueueFullMode.DropNewest:
                        return false;

                    case BoundedQueueFullMode.DropOldest:
                        // drop one item from head
                        _head = (_head + 1) % Capacity;
                        _count--;
                        break;

                    case BoundedQueueFullMode.OverwriteOldest:
                        // overwrite at tail by advancing head (keep count == Capacity)
                        _head = (_head + 1) % Capacity;
                        _count--;
                        break;
                }
            }

            _buffer[_tail] = item;
            _tail = (_tail + 1) % Capacity;
            _count++;
            return true;
        }
    }

    public bool TryDequeue(out T item)
    {
        lock (_lock)
        {
            if (_count == 0)
            {
                item = default!;
                return false;
            }

            item = _buffer[_head];
            _head = (_head + 1) % Capacity;
            _count--;
            return true;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _head = 0;
            _tail = 0;
            _count = 0;
        }
    }
}

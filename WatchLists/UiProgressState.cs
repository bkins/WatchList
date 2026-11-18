namespace WatchLists;

public sealed class UiProgressState : System.ComponentModel.INotifyPropertyChanged
{
    private static readonly Lazy<UiProgressState> State = new(() => new UiProgressState());
    private bool _isVisible;
    private string _statusText = string.Empty;
    private long _sequence;

    public static UiProgressState Current => State.Value;

    public bool IsVisible
    {
        get => _isVisible;
        private set
        {
            if (_isVisible == value)
            {
                return;
            }

            _isVisible = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsVisible)));
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set
        {
            if (_statusText == value)
            {
                return;
            }

            _statusText = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(StatusText)));
        }
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    public long Show(string statusText)
    {
        var sequence = Interlocked.Increment(ref _sequence);
        StatusText = statusText;
        IsVisible = true;
        return sequence;
    }

    public async Task HideAfterAsync(long sequence, int delayMilliseconds)
    {
        await Task.Delay(delayMilliseconds);
        if (Interlocked.Read(ref _sequence) == sequence)
        {
            IsVisible = false;
        }
    }
}

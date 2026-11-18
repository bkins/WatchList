using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;

namespace WatchLists;

public sealed class UniversalBusyOverlay : WindowOverlay, IDisposable
{
    private readonly BusySpinnerElement _spinnerElement = new();
    private readonly IDispatcherTimer _animationTimer;
    private readonly BusyOperationTracker _operationTracker;
    private readonly Window _window;
    private bool _disposed;

    public UniversalBusyOverlay(Window window, BusyOperationTracker operationTracker) : base(window)
    {
        _window           = window;
        _operationTracker = operationTracker;
        _animationTimer   = window.Dispatcher.CreateTimer();
        _animationTimer.Interval = TimeSpan.FromMilliseconds(50);
        _animationTimer.Tick += HandleAnimationTick;
        AddWindowElement(_spinnerElement);
        _operationTracker.BusyChanged += HandleBusyChanged;
        _window.Destroying += HandleWindowDestroying;
        SetBusy(_operationTracker.IsBusy);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _operationTracker.BusyChanged -= HandleBusyChanged;
        _window.Destroying -= HandleWindowDestroying;
        _animationTimer.Tick -= HandleAnimationTick;
        _animationTimer.Stop();
    }

    private void HandleBusyChanged(object? sender, bool isBusy)
    {
        if (!MainThread.IsMainThread)
        {
            MainThread.BeginInvokeOnMainThread(() => SetBusy(_operationTracker.IsBusy));
            return;
        }

        SetBusy(_operationTracker.IsBusy);
    }

    private void HandleAnimationTick(object? sender, EventArgs eventArgs)
    {
        _spinnerElement.Advance();
        Invalidate();
    }

    private void HandleWindowDestroying(object? sender, EventArgs eventArgs)
    {
        Dispose();
    }

    private void SetBusy(bool isBusy)
    {
        if (!MainThread.IsMainThread)
        {
            MainThread.BeginInvokeOnMainThread(() => SetBusy(isBusy));
            return;
        }

        IsVisible = isBusy;
        if (isBusy)
        {
            _animationTimer.Start();
        }
        else
        {
            _animationTimer.Stop();
        }

        Invalidate();
    }
}

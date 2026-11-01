using System.ComponentModel;
using System.Windows.Threading;

namespace SelaDock.ViewModel;

/// <summary>
/// Clock for the dock. Rather than ticking every second, it schedules a single timer that fires on
/// the next minute boundary, then re-arms. The dock therefore wakes about once a minute.
/// </summary>
public sealed class ClockViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly DispatcherTimer _timer;

    public string Time { get; private set; } = string.Empty;
    public string Date { get; private set; } = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ClockViewModel()
    {
        _timer = new DispatcherTimer();
        _timer.Tick += (_, _) => Update();
        Update();
    }

    private void Update()
    {
        var now = DateTime.Now;
        Time = now.ToString("HH:mm");
        Date = now.ToString("ddd, d MMM");
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Time)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Date)));

        // Re-arm for the start of the next minute (+ a small margin to avoid firing a second early).
        var next = now.AddMinutes(1);
        var boundary = new DateTime(next.Year, next.Month, next.Day, next.Hour, next.Minute, 0);
        _timer.Interval = boundary - now + TimeSpan.FromMilliseconds(50);
        _timer.Start();
    }

    public void Dispose() => _timer.Stop();
}

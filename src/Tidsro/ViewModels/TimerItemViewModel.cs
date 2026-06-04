using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tidsro.Models;
using Tidsro.Services;

namespace Tidsro.ViewModels;

public partial class TimerItemViewModel : ObservableObject
{
    private readonly SchedulerService _scheduler;
    public TimerItem Item { get; }

    [ObservableProperty] private string _remainingText = "00:00";
    [ObservableProperty] private bool _isPaused;

    public TimerItemViewModel(TimerItem item, SchedulerService scheduler)
    {
        Item = item; _scheduler = scheduler;
        Refresh();
    }

    public string? Label => Item.Label;
    public bool HasSound => Item.Sound != SoundChoice.None;
    public string SoundTag => HasSound ? "sound" : "silent";

    public void Refresh()
    {
        var r = _scheduler.Remaining(Item);
        RemainingText = r.Hours > 0 ? r.ToString(@"h\:mm\:ss") : r.ToString(@"mm\:ss");
        IsPaused = Item.State == TimerState.Paused;
    }

    [RelayCommand] private void PauseResume()
    {
        if (Item.State == TimerState.Running) _scheduler.Pause(Item);
        else if (Item.State == TimerState.Paused) _scheduler.Resume(Item);
        Refresh();
    }

    [RelayCommand] private void Cancel() => _scheduler.Cancel(Item);
}

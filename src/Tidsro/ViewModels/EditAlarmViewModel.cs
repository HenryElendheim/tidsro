using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tidsro.Models;
using Tidsro.Services;

namespace Tidsro.ViewModels;

/// <summary>
/// Backs the modal "Edit alarm" dialog. Pre-filled from the agenda row, validates the time on Save,
/// applies the edit through the injected callback, then raises <see cref="CloseRequested"/>.
/// Constructed with callbacks like <see cref="SettingsViewModel"/> — no scheduler reference of its own.
/// </summary>
public partial class EditAlarmViewModel : ObservableObject
{
    private readonly Guid _id;
    private readonly Action<Guid, int, int, string?, SoundChoice> _apply;
    private readonly ISoundService _sound;

    public SoundChoice[] SoundOptions { get; }

    [ObservableProperty] private string _timeInput;
    [ObservableProperty] private string _label;
    [ObservableProperty] private SoundChoice _selectedSound;
    [ObservableProperty] private string? _error;

    /// <summary>Raised when the dialog should close. true = saved, false = cancelled.</summary>
    public event EventHandler<bool>? CloseRequested;

    public EditAlarmViewModel(Guid id, string timeInput, string label, SoundChoice sound,
        SoundChoice[] soundOptions, Action<Guid, int, int, string?, SoundChoice> apply, ISoundService soundSvc)
    {
        _id = id;
        _timeInput = timeInput;
        _label = label;
        _selectedSound = sound;
        SoundOptions = soundOptions;
        _apply = apply;
        _sound = soundSvc;
    }

    // Save validates the time (same rules as the add path); a bad time shows the error and keeps the dialog open.
    [RelayCommand]
    private void Save()
    {
        if (!ClockTimeRules.TryParse(TimeInput, out var h, out var m, out var err)) { Error = err; return; }
        Error = null;
        _apply(_id, h, m, Label, SelectedSound);
        CloseRequested?.Invoke(this, true);
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(this, false);

    // Preview mirrors the main window's picker preview: disabled when the choice is silent.
    [RelayCommand(CanExecute = nameof(CanPreview))]
    private void PreviewSound() => _sound.Play(SelectedSound);
    private bool CanPreview() => SelectedSound != SoundChoice.None;

    partial void OnSelectedSoundChanged(SoundChoice value) => PreviewSoundCommand.NotifyCanExecuteChanged();
}

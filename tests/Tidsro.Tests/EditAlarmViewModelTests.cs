using Tidsro.Models;
using Tidsro.ViewModels;
using Xunit;

namespace Tidsro.Tests;

public class EditAlarmViewModelTests
{
    private static readonly SoundChoice[] Options =
        { SoundChoice.None, SoundChoice.SoftChime, SoundChoice.Marimba, SoundChoice.Bell };

    private static EditAlarmViewModel New(string timeInput, out List<(Guid id, int h, int m, string? label, SoundChoice sound)> applied,
        out FakeSoundService sound, Guid? id = null)
    {
        var captured = new List<(Guid, int, int, string?, SoundChoice)>();
        applied = captured;
        sound = new FakeSoundService();
        return new EditAlarmViewModel(id ?? Guid.NewGuid(), timeInput, "Tea", SoundChoice.Bell,
            Options, (i, h, m, l, s) => captured.Add((i, h, m, l, s)), sound);
    }

    [Fact]
    public void Save_with_a_valid_time_applies_once_and_requests_close_with_saved_true()
    {
        var id = Guid.NewGuid();
        var vm = New("11:15", out var applied, out _, id);
        bool? closedSaved = null;
        vm.CloseRequested += (_, saved) => closedSaved = saved;

        vm.SaveCommand.Execute(null);

        var call = Assert.Single(applied);
        Assert.Equal(id, call.id);
        Assert.Equal(11, call.h);
        Assert.Equal(15, call.m);
        Assert.Equal(SoundChoice.Bell, call.sound);
        Assert.Null(vm.Error);
        Assert.Equal(true, closedSaved);   // dialog closes as saved
    }

    [Fact]
    public void Save_with_an_invalid_time_sets_the_error_and_does_not_apply_or_close()
    {
        var vm = New("99:99", out var applied, out _);
        var closeRaised = false;
        vm.CloseRequested += (_, _) => closeRaised = true;

        vm.SaveCommand.Execute(null);

        Assert.Empty(applied);          // edit not applied
        Assert.NotNull(vm.Error);       // message shown, dialog stays open
        Assert.False(closeRaised);
    }

    [Fact]
    public void Cancel_requests_close_with_saved_false_and_does_not_apply()
    {
        var vm = New("11:15", out var applied, out _);
        bool? closedSaved = null;
        vm.CloseRequested += (_, saved) => closedSaved = saved;

        vm.CancelCommand.Execute(null);

        Assert.Empty(applied);
        Assert.Equal(false, closedSaved);
    }

    [Fact]
    public void Preview_is_gated_on_the_selected_sound()
    {
        var vm = New("11:15", out _, out var sound);
        vm.SelectedSound = SoundChoice.None;
        Assert.False(vm.PreviewSoundCommand.CanExecute(null));

        vm.SelectedSound = SoundChoice.Marimba;
        Assert.True(vm.PreviewSoundCommand.CanExecute(null));
        vm.PreviewSoundCommand.Execute(null);
        Assert.Equal(SoundChoice.Marimba, sound.LastPlayed);
    }
}

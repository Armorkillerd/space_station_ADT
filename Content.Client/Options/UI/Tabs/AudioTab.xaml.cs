using Content.Client.Administration.Managers;
using Content.Client.Audio;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Content.Shared.Corvax.CCCVars;
using Robust.Client.Audio;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.XAML;
using Robust.Shared;
using Robust.Shared.Configuration;
using Content.Shared.ADT.CCVar;

namespace Content.Client.Options.UI.Tabs;

[GenerateTypedNameReferences]
public sealed partial class AudioTab : Control
{
    [Dependency] private readonly IAudioManager _audio = default!;
    [Dependency] private readonly IClientAdminManager _admin = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    public AudioTab()
    {
        RobustXamlLoader.Load(this);
        IoCManager.InjectDependencies(this);

        var masterVolume = Control.AddOptionPercentSlider(
            CVars.AudioMasterVolume,
            SliderVolumeMaster,
            scale: ContentAudioSystem.MasterVolumeMultiplier);
        masterVolume.ImmediateValueChanged += OnMasterVolumeSliderChanged;

        // Corvax-TTS-Start
        Control.AddOptionPercentSlider(
            CCCVars.TTSVolume,
            SliderVolumeTts,
            scale: ContentAudioSystem.TtsMultiplier);
        // Corvax-TTS-End

        // ADT Barks start
        Control.AddOptionPercentSlider(
            ADTCCVars.BarksVolume,
            SliderVolumeBarks,
            scale: ContentAudioSystem.BarksMultiplier);

        Control.AddOptionDropDown<bool>(
            ADTCCVars.ReplaceTTSWithBarks,
            DropDownBarksOrTTS,
            [
                new OptionDropDownCVar<bool>.ValueOption(true, Loc.GetString("ui-options-barks-speech")),
                new OptionDropDownCVar<bool>.ValueOption(false, Loc.GetString("ui-options-tts-speech")),
            ]);

        // ADT Barks end
        Control.AddOptionPercentSlider(
            CVars.MidiVolume,
            SliderVolumeMidi,
            scale: ContentAudioSystem.MidiVolumeMultiplier);

        Control.AddOptionPercentSlider(
            CCVars.AmbientMusicVolume,
            SliderVolumeAmbientMusic,
            scale: ContentAudioSystem.AmbientMusicMultiplier);

        Control.AddOptionPercentSlider(
            CCVars.AmbienceVolume,
            SliderVolumeAmbience,
            scale: ContentAudioSystem.AmbienceMultiplier);

        Control.AddOptionPercentSlider(
            CCVars.LobbyMusicVolume,
            SliderVolumeLobby,
            scale: ContentAudioSystem.LobbyMultiplier);

        Control.AddOptionPercentSlider(
            CCVars.InterfaceVolume,
            SliderVolumeInterface,
            scale: ContentAudioSystem.InterfaceMultiplier);

        Control.AddOptionSlider(
            CCVars.MaxAmbientSources,
            SliderMaxAmbienceSounds,
            _cfg.GetCVar(CCVars.MinMaxAmbientSourcesConfigured),
            _cfg.GetCVar(CCVars.MaxMaxAmbientSourcesConfigured));

        Control.AddOptionCheckBox(CCVars.LobbyMusicEnabled, LobbyMusicCheckBox);
        Control.AddOptionCheckBox(CCVars.RestartSoundsEnabled, RestartSoundsCheckBox);
        Control.AddOptionCheckBox(CCVars.EventMusicEnabled, EventMusicCheckBox);
        Control.AddOptionCheckBox(CCVars.AdminSoundsEnabled, AdminSoundsCheckBox);
        Control.AddOptionCheckBox(CCVars.BwoinkSoundEnabled, BwoinkSoundCheckBox);

        Control.Initialize();
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();
        _admin.AdminStatusUpdated += UpdateAdminButtonsVisibility;
        UpdateAdminButtonsVisibility();
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();
        _admin.AdminStatusUpdated -= UpdateAdminButtonsVisibility;
    }


    private void UpdateAdminButtonsVisibility()
    {
        BwoinkSoundCheckBox.Visible = _admin.IsActive() && _admin.HasFlag(AdminFlags.Permissions); // ADT-Tweak
    }

    private void OnMasterVolumeSliderChanged(float value)
    {
        // TODO: I was thinking of giving OptionsTabControlRow a flag to "set CVar immediately", but I'm deferring that
        // until there's a proper system for enforcing people don't close the window with pending changes.
        _audio.SetMasterGain(value);
    }
}

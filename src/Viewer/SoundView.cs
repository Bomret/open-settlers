using Godot;
using OpenSettlers.Assets;

namespace OpenSettlers.Viewer;

/// <summary>The view of a sound: a play button. The 8-bit stream of Godot is signed, so the samples are converted.</summary>
public sealed class SoundView : PreviewView
{
    /// <inheritdoc/>
    public override System.Type PreviewType => typeof(SoundPreview);

    /// <inheritdoc/>
    public override Control Create(Preview preview)
    {
        var sound = ((SoundPreview)preview).Sound;
        var player = new AudioStreamPlayer
        {
            Stream = new AudioStreamWav
            {
                Format = AudioStreamWav.FormatEnum.Format8Bits,
                MixRate = sound.SampleRate,
                Stereo = false,
                Data = sound.SignedSamples(),
            },
        };
        var play = new Button { Text = "Play" };
        play.Pressed += () => player.Play();
        var box = new VBoxContainer();
        box.AddChild(new Label { Text = preview.Summary });
        box.AddChild(play);
        box.AddChild(player);

        return box;
    }
}

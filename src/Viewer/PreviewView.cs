using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using OpenSettlers.Assets;

namespace OpenSettlers.Viewer;

/// <summary>
/// The Godot view of one <see cref="Preview"/> type, whichever asset kinds produce it. A view is a class with a
/// parameterless constructor in its own file; <see cref="Discover"/> finds it, so adding a view edits no shared
/// list. It lives here because <see cref="AssetKind"/> is Godot-free. A preview type without a view shows its
/// summary as text.
/// </summary>
public abstract class PreviewView
{
    /// <summary>The <see cref="Preview"/> type this view draws.</summary>
    public abstract Type PreviewType { get; }

    /// <summary>Builds the control that shows <c>preview</c>, which is of this view's <see cref="PreviewType"/>.</summary>
    public abstract Control Create(Preview preview);

    /// <summary>
    /// A picture of <c>width</c> by <c>height</c> RGBA pixels under its summary, scaled to fill the pane with its
    /// aspect ratio kept and its pixels kept sharp. A <c>backdrop</c> is drawn behind the picture alone, so it shows
    /// the transparent pixels and the picture's bounds.
    /// </summary>
    protected static Control Picture(string summary, int width, int height, byte[] rgba, Color? backdrop = null)
    {
        var image = Image.CreateFromData(width, height, false, Image.Format.Rgba8, rgba);
        var picture = new TextureRect
        {
            Texture = ImageTexture.CreateFromImage(image),
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
        };
        var fit = new AspectRatioContainer
        {
            Ratio = (float)width / height,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        if (backdrop is { } color)
        {
            var rect = new ColorRect { Color = color };
            picture.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            rect.AddChild(picture);
            fit.AddChild(rect);
        }
        else
        {
            fit.AddChild(picture);
        }

        var box = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        box.AddChild(new Label { Text = summary });
        box.AddChild(fit);

        return box;
    }

    /// <summary>One instance of every view. Two views of one preview type are the browser's <see cref="PreviewClash"/>.</summary>
    public static IReadOnlyList<PreviewView> Discover() =>
        typeof(PreviewView)
            .Assembly.GetTypes()
            .Where(t =>
                t.IsSubclassOf(typeof(PreviewView)) && !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) != null
            )
            .Select(t => (PreviewView)Activator.CreateInstance(t)!)
            .ToList();

    /// <summary>The problem with <c>views</c>, or null when no two draw one preview type.</summary>
    public static string? Clash(IEnumerable<PreviewView> views) =>
        PreviewClash.Of(views.Select(v => (v.GetType().Name, v.PreviewType)));
}

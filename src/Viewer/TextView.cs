using Godot;
using OpenSettlers.Assets;

namespace OpenSettlers.Viewer;

/// <summary>The view of the texts: a table with the key in the first column and one value column per language.</summary>
public sealed class TextView : PreviewView
{
    /// <inheritdoc/>
    public override System.Type PreviewType => typeof(TextPreview);

    /// <inheritdoc/>
    public override Control Create(Preview preview)
    {
        var table = (TextPreview)preview;
        var tree = new Tree
        {
            Columns = table.Languages.Count + 1,
            ColumnTitlesVisible = true,
            HideRoot = true,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        tree.SetColumnTitle(0, "key");
        for (var i = 0; i < table.Languages.Count; i++)
        {
            tree.SetColumnTitle(i + 1, table.Languages[i]);
        }

        var root = tree.CreateItem();
        foreach (var row in table.Rows)
        {
            var item = tree.CreateItem(root);
            item.SetText(0, row.Key);
            for (var i = 0; i < row.Values.Count; i++)
            {
                item.SetText(i + 1, row.Values[i] ?? "");
            }
        }

        var box = new VBoxContainer();
        box.AddChild(new Label { Text = preview.Summary });
        box.AddChild(tree);

        return box;
    }
}

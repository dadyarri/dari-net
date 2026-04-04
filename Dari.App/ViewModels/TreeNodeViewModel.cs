using CommunityToolkit.Mvvm.ComponentModel;
using Dari.App.Models;

namespace Dari.App.ViewModels;

/// <summary>Base class for tree-view nodes shown in directory-tree mode.</summary>
public abstract class TreeNodeViewModel : ObservableObject { }

/// <summary>Represents a directory node in the tree view.</summary>
public sealed partial class DirectoryNodeViewModel : TreeNodeViewModel
{
    [ObservableProperty]
    private bool _isExpanded = true;

    [ObservableProperty]
    private bool _isSelected;

    public string Name { get; }

    /// <summary>Full forward-slash-delimited path including trailing slash (e.g. <c>"a/b/"</c>).</summary>
    public string FullPath { get; }

    public List<TreeNodeViewModel> Children { get; } = [];

    public DirectoryNodeViewModel(string name, string fullPath = "")
    {
        Name = name;
        FullPath = fullPath;
    }

    partial void OnIsSelectedChanged(bool value)
    {
        // Propagate selection to all children (recursive).
        foreach (var child in Children)
        {
            switch (child)
            {
                case FileNodeViewModel file:
                    file.Entry.IsSelected = value;
                    break;
                case DirectoryNodeViewModel dir:
                    dir.IsSelected = value;
                    break;
            }
        }
    }
}

/// <summary>Represents a file leaf node in the tree view.</summary>
public sealed class FileNodeViewModel : TreeNodeViewModel
{
    public ArchiveEntryViewModel Entry { get; }

    public FileNodeViewModel(ArchiveEntryViewModel entry) => Entry = entry;
}

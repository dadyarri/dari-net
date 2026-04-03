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

    public string Name { get; }

    public List<TreeNodeViewModel> Children { get; } = [];

    public DirectoryNodeViewModel(string name) => Name = name;
}

/// <summary>Represents a file leaf node in the tree view.</summary>
public sealed class FileNodeViewModel : TreeNodeViewModel
{
    public ArchiveEntryViewModel Entry { get; }

    public FileNodeViewModel(ArchiveEntryViewModel entry) => Entry = entry;
}

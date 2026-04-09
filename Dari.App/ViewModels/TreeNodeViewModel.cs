using CommunityToolkit.Mvvm.ComponentModel;
using Dari.App.Models;

namespace Dari.App.ViewModels;

/// <summary>Base class for tree-view nodes shown in directory-tree mode.</summary>
public abstract class TreeNodeViewModel : ObservableObject { }

/// <summary>Represents a directory node in the tree view.</summary>
public sealed partial class DirectoryNodeViewModel : TreeNodeViewModel
{
    /// <summary>
    /// Re-entrance guard for <see cref="OnIsSelectedChanged"/> and
    /// <see cref="UpdateSelectionStateFromChildren"/>: when > 0 the handler suppresses
    /// child-propagation so that bottom-up recomputation cannot trigger another top-down walk.
    /// </summary>
    private int _suppressCount;

    [ObservableProperty]
    private bool _isExpanded = true;

    /// <summary>
    /// Tri-state selection: <c>true</c> = all children selected,
    /// <c>null</c> = partially selected (indeterminate), <c>false</c> = none selected.
    /// </summary>
    [ObservableProperty]
    private bool? _isSelected;

    public string Name { get; }

    /// <summary>Full forward-slash-delimited path including trailing slash (e.g. <c>"a/b/"</c>).</summary>
    public string FullPath { get; }

    public List<TreeNodeViewModel> Children { get; } = [];

    public DirectoryNodeViewModel(string name, string fullPath = "")
    {
        Name = name;
        FullPath = fullPath;
    }

    partial void OnIsSelectedChanged(bool? value)
    {
        if (_suppressCount > 0) return;

        // Propagate a definite true/false click to all descendants.
        // Indeterminate state is never set by the user — it is computed bottom-up.
        _suppressCount++;
        try
        {
            foreach (var child in Children)
            {
                switch (child)
                {
                    case FileNodeViewModel file:
                        file.Entry.IsSelected = value == true;
                        break;
                    case DirectoryNodeViewModel dir:
                        dir.IsSelected = value;
                        break;
                }
            }
        }
        finally
        {
            _suppressCount--;
        }
    }

    /// <summary>
    /// Recomputes this directory's <see cref="IsSelected"/> state from the current state
    /// of its immediate children without triggering further child propagation.
    /// Callers should update sub-directories depth-first so that each directory's state
    /// already reflects its children before this method is called on the parent.
    /// </summary>
    internal void UpdateSelectionStateFromChildren()
    {
        if (Children.Count == 0)
        {
            _suppressCount++;
            IsSelected = false;
            _suppressCount--;
            return;
        }

        bool anySelected = false, anyUnselected = false;
        foreach (var child in Children)
        {
            bool? state = child switch
            {
                FileNodeViewModel f => (bool?)f.Entry.IsSelected,
                DirectoryNodeViewModel d => d.IsSelected,
                _ => false,
            };

            switch (state)
            {
                case true:
                    anySelected = true;
                    break;
                case false:
                    anyUnselected = true;
                    break;
                case null:
                    anySelected = true;
                    anyUnselected = true;
                    break;
            }
        }

        bool? newState = (anySelected, anyUnselected) switch
        {
            (true, false) => true,
            (false, _) => false,
            _ => null, // mixed: indeterminate
        };

        _suppressCount++;
        IsSelected = newState;
        _suppressCount--;
    }
}

/// <summary>Represents a file leaf node in the tree view.</summary>
public sealed class FileNodeViewModel : TreeNodeViewModel
{
    public ArchiveEntryViewModel Entry { get; }

    public FileNodeViewModel(ArchiveEntryViewModel entry) => Entry = entry;
}

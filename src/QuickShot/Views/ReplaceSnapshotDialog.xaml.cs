using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QuickShot.Models;
using QuickShot.Services;

namespace QuickShot.Views;

/// <summary>
/// Shown when the floating snapshot limit is reached: the user picks which existing
/// snapshot the new capture replaces, or cancels (and the new capture is discarded).
/// </summary>
public partial class ReplaceSnapshotDialog : Window
{
    private ReplaceSnapshotDialog(SnapshotManager manager)
    {
        InitializeComponent();

        var loc = LocalizationService.Instance;
        var oldFirst = manager.OldestFirst();

        MessageText.Text = loc.Format("Replace_Message", oldFirst.Count, manager.MaxCount);

        Items = oldFirst
            .Select((snapshot, index) => new Choice
            {
                Snapshot = snapshot,
                Caption = loc.Format("Replace_Item", index + 1, snapshot.TimeText, snapshot.SizeText),
            })
            .ToList();

        List.ItemsSource = Items;
        if (Items.Count > 0) List.SelectedIndex = 0;

        Loaded += (_, _) => List.Focus();
    }

    /// <summary>Holds the ordered list, oldest first, so "replace the oldest" is the default.</summary>
    public IReadOnlyList<Choice> Items { get; }

    public Snapshot? Selected { get; private set; }

    public sealed class Choice
    {
        public required Snapshot Snapshot { get; init; }

        public required string Caption { get; init; }
    }

    /// <summary>Returns the snapshot to drop, or null when the user cancelled.</summary>
    public static Snapshot? Prompt(SnapshotManager manager)
    {
        var dialog = new ReplaceSnapshotDialog(manager);
        return dialog.ShowDialog() == true ? dialog.Selected : null;
    }

    private void OnReplaceClick(object sender, RoutedEventArgs e) => Accept();

    private void OnListDoubleClick(object sender, MouseButtonEventArgs e) => Accept();

    private void Accept()
    {
        if (List.SelectedItem is not Choice choice) return;
        Selected = choice.Snapshot;
        DialogResult = true;
    }
}

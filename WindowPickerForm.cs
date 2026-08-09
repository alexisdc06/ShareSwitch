using System.Drawing;
using System.Windows.Forms;

namespace ShareSwitch;

/// <summary>
/// Modal list of capturable windows. Alternative to hold-to-select for
/// occluded/minimized windows or hook-averse environments.
/// </summary>
public sealed class WindowPickerForm : Form
{
    private readonly ListView _list;
    private readonly Button _okButton;

    public WindowEnumerator.WindowInfo? SelectedWindow { get; private set; }

    public WindowPickerForm()
    {
        Text = "ShareSwitch — Select Window";
        Size = new Size(560, 480);
        MinimumSize = new Size(420, 320);
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = false;
        MinimizeBox = false;

        _list = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            MultiSelect = false,
            Font = new Font("Segoe UI", 10f)
        };
        _list.Columns.Add("Window", 360);
        _list.Columns.Add("Process", 140);
        _list.SelectedIndexChanged += (_, _) => _okButton!.Enabled = _list.SelectedItems.Count > 0;
        _list.DoubleClick += (_, _) => CommitSelection();

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 44,
            Padding = new Padding(8)
        };

        var cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        _okButton = new Button { Text = "Select", Enabled = false, AutoSize = true };
        _okButton.Click += (_, _) => CommitSelection();
        var refreshButton = new Button { Text = "Refresh", AutoSize = true };
        refreshButton.Click += (_, _) => RefreshList();

        buttonPanel.Controls.Add(cancelButton);
        buttonPanel.Controls.Add(_okButton);
        buttonPanel.Controls.Add(refreshButton);

        Controls.Add(_list);
        Controls.Add(buttonPanel);

        AcceptButton = _okButton;
        CancelButton = cancelButton;

        Load += (_, _) => RefreshList();
        Resize += (_, _) => _list.Columns[0].Width = Math.Max(160, _list.ClientSize.Width - _list.Columns[1].Width - 4);
    }

    private void RefreshList()
    {
        _list.BeginUpdate();
        _list.Items.Clear();

        foreach (var w in WindowEnumerator.GetCaptureCandidates())
        {
            var item = new ListViewItem(w.Title) { Tag = w };
            item.SubItems.Add(w.ProcessName);
            _list.Items.Add(item);
        }

        _list.EndUpdate();
        _okButton.Enabled = false;
    }

    private void CommitSelection()
    {
        if (_list.SelectedItems.Count == 0) return;
        SelectedWindow = (WindowEnumerator.WindowInfo)_list.SelectedItems[0].Tag!;
        DialogResult = DialogResult.OK;
        Close();
    }
}

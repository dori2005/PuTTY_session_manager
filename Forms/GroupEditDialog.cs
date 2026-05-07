namespace PuttySessionManager.Forms;

public class GroupEditDialog : Form
{
    public string GroupName => _textBox.Text.Trim();

    private readonly TextBox _textBox;

    public GroupEditDialog(string prompt, string defaultValue = "")
    {
        Text            = "그룹";
        Size            = new Size(320, 140);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        MinimizeBox     = false;
        StartPosition   = FormStartPosition.CenterParent;

        var label     = new Label  { Text = prompt,        Left = 12, Top = 12, Width = 280, AutoSize = false };
        _textBox      = new TextBox{ Text = defaultValue,  Left = 12, Top = 34, Width = 280 };
        var btnOk     = new Button { Text = "확인",         Left = 120, Top = 64, Width = 80, DialogResult = DialogResult.OK };
        var btnCancel = new Button { Text = "취소",         Left = 210, Top = 64, Width = 80, DialogResult = DialogResult.Cancel };

        AcceptButton = btnOk;
        CancelButton = btnCancel;
        Controls.AddRange(new Control[] { label, _textBox, btnOk, btnCancel });
    }
}

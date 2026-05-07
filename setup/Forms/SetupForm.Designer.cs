namespace SetupApp.Forms;

partial class SetupForm
{
    private System.ComponentModel.IContainer components = null!;

    private Panel           _panelHeader  = null!;
    private Label           _lblTitle     = null!;
    private Label           _lblVersion   = null!;
    private Panel           _panelContent = null!;
    private Label           _lblPathLabel = null!;
    private TextBox         _txtPath      = null!;
    private Button          _btnBrowse    = null!;
    private CheckBox        _chkDesktop   = null!;
    private CheckBox        _chkStartMenu = null!;
    private Panel           _panelFooter  = null!;
    private ProgressBar     _progressBar  = null!;
    private Label           _lblStatus    = null!;
    private Button          _btnInstall   = null!;
    private Button          _btnCancel    = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();

        // ── Header ──────────────────────────────────────────────────
        _panelHeader = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 80,
            BackColor = Color.FromArgb(30, 30, 30)
        };

        _lblTitle = new Label
        {
            Text      = "PuTTY Session Manager",
            ForeColor = Color.White,
            Font      = new Font("Segoe UI", 14f, FontStyle.Bold),
            Location  = new Point(20, 14),
            AutoSize  = true
        };

        _lblVersion = new Label
        {
            Text      = "버전 1.0.0",
            ForeColor = Color.FromArgb(160, 160, 160),
            Font      = new Font("Segoe UI", 9f),
            Location  = new Point(22, 48),
            AutoSize  = true
        };

        _panelHeader.Controls.AddRange(new Control[] { _lblTitle, _lblVersion });

        // ── Content ─────────────────────────────────────────────────
        _panelContent = new Panel
        {
            Dock    = DockStyle.Fill,
            Padding = new Padding(20)
        };

        _lblPathLabel = new Label
        {
            Text     = "설치 경로:",
            Location = new Point(20, 24),
            AutoSize = true,
            Font     = new Font("Segoe UI", 9.5f)
        };

        _txtPath = new TextBox
        {
            Location = new Point(20, 46),
            Width    = 340,
            Font     = new Font("Segoe UI", 9.5f)
        };

        _btnBrowse = new Button
        {
            Text     = "찾기...",
            Location = new Point(366, 44),
            Width    = 70,
            Height   = 28
        };

        _chkDesktop = new CheckBox
        {
            Text     = "바탕화면 바로가기 만들기",
            Location = new Point(20, 90),
            AutoSize = true,
            Checked  = true,
            Font     = new Font("Segoe UI", 9.5f)
        };

        _chkStartMenu = new CheckBox
        {
            Text     = "시작 메뉴에 추가",
            Location = new Point(20, 118),
            AutoSize = true,
            Checked  = true,
            Font     = new Font("Segoe UI", 9.5f)
        };

        _panelContent.Controls.AddRange(new Control[]
        {
            _lblPathLabel, _txtPath, _btnBrowse, _chkDesktop, _chkStartMenu
        });

        // ── Footer ──────────────────────────────────────────────────
        _panelFooter = new Panel
        {
            Dock      = DockStyle.Bottom,
            Height    = 80,
            BackColor = Color.FromArgb(245, 245, 245),
            Padding   = new Padding(16)
        };

        _progressBar = new ProgressBar
        {
            Location = new Point(16, 10),
            Width    = 420,
            Height   = 18,
            Visible  = false
        };

        _lblStatus = new Label
        {
            Text      = "",
            Location  = new Point(16, 32),
            Width     = 300,
            ForeColor = Color.Gray,
            Font      = new Font("Segoe UI", 8.5f)
        };

        _btnInstall = new Button
        {
            Text     = "설치",
            Location = new Point(340, 24),
            Width    = 80,
            Height   = 30,
            Font     = new Font("Segoe UI", 9.5f),
            BackColor = Color.FromArgb(0, 120, 215),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        _btnInstall.FlatAppearance.BorderSize = 0;

        _btnCancel = new Button
        {
            Text     = "취소",
            Location = new Point(426, 24),
            Width    = 80,
            Height   = 30,
            Font     = new Font("Segoe UI", 9.5f)
        };

        _panelFooter.Controls.AddRange(new Control[]
        {
            _progressBar, _lblStatus, _btnInstall, _btnCancel
        });

        // ── Form ────────────────────────────────────────────────────
        Text            = "PuTTY Session Manager 설치";
        Size            = new Size(540, 320);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        MinimizeBox     = false;
        StartPosition   = FormStartPosition.CenterScreen;

        Controls.Add(_panelContent);
        Controls.Add(_panelFooter);
        Controls.Add(_panelHeader);

        // ── 이벤트 ──────────────────────────────────────────────────
        _btnBrowse.Click  += BtnBrowse_Click;
        _btnInstall.Click += BtnInstall_Click;
        _btnCancel.Click  += (_, _) => Close();
    }
}

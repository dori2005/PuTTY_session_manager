namespace PuttySessionManager.Forms;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null!;

    private ToolStrip       _toolStrip      = null!;
    private ToolStripButton _btnNewGroup    = null!;
    private ToolStripButton _btnRefresh     = null!;
    private TreeView        _treeView       = null!;
    private StatusStrip     _statusStrip    = null!;
    private ToolStripStatusLabel _statusLabel = null!;
    private ImageList       _imageList      = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null) components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components   = new System.ComponentModel.Container();
        _imageList   = new ImageList(components) { ColorDepth = ColorDepth.Depth32Bit };

        // -- ToolStrip --
        _toolStrip   = new ToolStrip();
        _btnNewGroup = new ToolStripButton("새 그룹") { ToolTipText = "새 그룹 만들기" };
        _btnRefresh  = new ToolStripButton("새로고침 (F5)") { ToolTipText = "PuTTY 세션 목록 새로고침" };

        _toolStrip.Items.AddRange(new ToolStripItem[]
        {
            _btnNewGroup,
            new ToolStripSeparator(),
            _btnRefresh
        });

        // -- TreeView --
        _treeView = new TreeView
        {
            Dock          = DockStyle.Fill,
            ShowLines     = true,
            ShowPlusMinus = true,
            HideSelection = false,
            ImageList     = _imageList,
            Font          = new Font("Segoe UI", 9.5f),
            AllowDrop     = true
        };

        // -- StatusStrip --
        _statusStrip = new StatusStrip();
        _statusLabel = new ToolStripStatusLabel("준비") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        _statusStrip.Items.Add(_statusLabel);

        // -- Form --
        Text          = "PuTTY Session Manager";
        Size          = new Size(380, 600);
        MinimumSize   = new Size(300, 400);
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview    = true;

        Controls.Add(_treeView);
        Controls.Add(_toolStrip);
        Controls.Add(_statusStrip);

        // -- 이벤트 --
        _btnNewGroup.Click          += BtnNewGroup_Click;
        _btnRefresh.Click           += BtnRefresh_Click;
        _treeView.NodeMouseDoubleClick += TreeView_NodeMouseDoubleClick;
        _treeView.NodeMouseClick    += TreeView_NodeMouseClick;
        _treeView.AfterExpand       += TreeView_AfterExpandCollapse;
        _treeView.AfterCollapse     += TreeView_AfterExpandCollapse;
        _treeView.ItemDrag          += TreeView_ItemDrag;
        _treeView.DragEnter         += TreeView_DragEnter;
        _treeView.DragOver          += TreeView_DragOver;
        _treeView.DragDrop          += TreeView_DragDrop;
        _treeView.DragLeave         += TreeView_DragLeave;
        _treeView.MouseDown         += TreeView_MouseDown;
        _treeView.MouseMove         += TreeView_MouseMove;
        _treeView.MouseUp           += TreeView_MouseUp;
        KeyDown                     += MainForm_KeyDown;
    }
}

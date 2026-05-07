using PuttySessionManager.Models;
using PuttySessionManager.Services;

namespace PuttySessionManager.Forms;

// X 버튼 = 진짜 종료.
// FormClosing 오버라이드 없음, NotifyIcon 없음 → WinForms 기본 동작 그대로.
public partial class MainForm : Form
{
    private readonly GroupStorageService _storage  = new();
    private readonly PuttyLaunchService  _launcher = new();

    private AppData            _appData     = new();
    private List<PuttySession> _allSessions = new();

    // TreeView 노드 구분 태그
    private enum NodeType { Group, Session, Ungrouped }
    private record NodeTag(NodeType Type, object? Data);

    // 드래그 중 하이라이트된 노드 추적
    private TreeNode? _dragTarget;

    public MainForm() => InitializeComponent();

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        LoadIcons();
        _appData     = _storage.Load();
        _allSessions = RegistryService.GetAllSessions();
        RefreshTree();
        UpdateStatus();
    }

    // ── 아이콘 ──────────────────────────────────────────────────────
    private void LoadIcons()
    {
        _imageList.ImageSize = new Size(16, 16);
        _imageList.Images.Add("group",   DrawGroupIcon());
        _imageList.Images.Add("session", DrawSessionIcon());

        // 폼/작업표시줄 아이콘 (exe에 내장된 리소스에서 로드)
        var stream = typeof(MainForm).Assembly
            .GetManifestResourceStream("PuttySessionManager.Resources.app.ico");
        if (stream is not null) Icon = new Icon(stream);
    }

    private static Bitmap DrawGroupIcon()
    {
        var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        // 폴더 몸통
        using var fill = new SolidBrush(Color.FromArgb(255, 230, 165, 30));
        g.FillRectangle(fill, 0, 4, 16, 10);
        // 폴더 탭
        using var tab = new SolidBrush(Color.FromArgb(255, 250, 190, 60));
        g.FillRectangle(tab, 0, 2, 7, 3);
        // 테두리
        using var pen = new Pen(Color.FromArgb(180, 160, 100, 0), 1);
        g.DrawRectangle(pen, 0, 4, 15, 9);
        return bmp;
    }

    private static Bitmap DrawSessionIcon()
    {
        var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        // 터미널 배경
        using var bg = new SolidBrush(Color.FromArgb(255, 20, 20, 20));
        g.FillRectangle(bg, 0, 0, 16, 16);
        // 테두리
        using var pen = new Pen(Color.FromArgb(255, 0, 180, 0), 1);
        g.DrawRectangle(pen, 0, 0, 15, 15);
        // ">" 프롬프트
        using var font  = new Font("Consolas", 7.5f, FontStyle.Bold);
        using var brush = new SolidBrush(Color.FromArgb(255, 0, 210, 0));
        g.DrawString(">", font, brush, 1f, 2f);
        return bmp;
    }

    // ── TreeView 구성 ────────────────────────────────────────────────
    private void RefreshTree()
    {
        _treeView.BeginUpdate();
        _treeView.Nodes.Clear();

        // 1. 사용자 정의 그룹
        foreach (var group in _appData.Groups)
        {
            var node = BuildGroupNode(group);
            _treeView.Nodes.Add(node);
            if (group.IsExpanded) node.Expand();
        }

        // 2. 미분류 세션
        var grouped      = _appData.Groups.SelectMany(g => g.SessionNames).ToHashSet();
        var ungrouped    = _allSessions.Where(s => !grouped.Contains(s.RegistryName)).ToList();

        if (ungrouped.Count > 0)
        {
            var ung = new TreeNode($"미분류 ({ungrouped.Count})")
            {
                Tag      = new NodeTag(NodeType.Ungrouped, null),
                ImageKey = "group", SelectedImageKey = "group"
            };
            foreach (var s in ungrouped)
                ung.Nodes.Add(BuildSessionNode(s));

            _treeView.Nodes.Add(ung);
            if (_appData.UngroupedExpanded) ung.Expand();
        }

        _treeView.EndUpdate();
    }

    private TreeNode BuildGroupNode(SessionGroup group)
    {
        var valid = group.SessionNames
            .Select(n => _allSessions.FirstOrDefault(s => s.RegistryName == n))
            .OfType<PuttySession>()
            .ToList();

        var node = new TreeNode($"{group.Name} ({valid.Count})")
        {
            Tag      = new NodeTag(NodeType.Group, group.Id),
            ImageKey = "group", SelectedImageKey = "group"
        };
        foreach (var s in valid)
            node.Nodes.Add(BuildSessionNode(s));

        return node;
    }

    private static TreeNode BuildSessionNode(PuttySession s) =>
        new(s.DisplayName)
        {
            Tag             = new NodeTag(NodeType.Session, s.RegistryName),
            ImageKey        = "session",
            SelectedImageKey = "session"
        };

    // ── 이벤트 핸들러 ────────────────────────────────────────────────
    private void BtnNewGroup_Click(object? sender, EventArgs e)
    {
        using var dlg = new GroupEditDialog("새 그룹 이름:");
        if (dlg.ShowDialog() != DialogResult.OK || string.IsNullOrWhiteSpace(dlg.GroupName)) return;

        _appData.Groups.Add(new SessionGroup { Name = dlg.GroupName });
        SaveAndRefresh();
    }

    private void BtnRefresh_Click(object? sender, EventArgs e)
    {
        _allSessions = RegistryService.GetAllSessions();
        RefreshTree();
        UpdateStatus();
    }

    private void MainForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.F5) BtnRefresh_Click(null, EventArgs.Empty);
    }

    private void TreeView_NodeMouseDoubleClick(object? sender, TreeNodeMouseClickEventArgs e)
    {
        if (e.Node?.Tag is not NodeTag { Type: NodeType.Session } tag) return;

        var session = _allSessions.FirstOrDefault(s => s.RegistryName == (string)tag.Data!);
        if (session is null) return;

        if (!_launcher.Launch(session.DisplayName))
            ShowPuttyNotFound();
    }

    private void TreeView_NodeMouseClick(object? sender, TreeNodeMouseClickEventArgs e)
    {
        if (e.Button != MouseButtons.Right) return;
        if (e.Node is not { } node) return;
        _treeView.SelectedNode = node;
        ShowContextMenu(node, e.Location);
    }

    private void TreeView_AfterExpandCollapse(object? sender, TreeViewEventArgs e)
    {
        if (e.Node is not { } node) return;
        if (node.Tag is not NodeTag tag) return;

        if (tag.Type == NodeType.Group)
        {
            var group = _appData.Groups.FirstOrDefault(g => g.Id.Equals(tag.Data));
            if (group is not null) group.IsExpanded = node.IsExpanded;
        }
        else if (tag.Type == NodeType.Ungrouped)
        {
            _appData.UngroupedExpanded = node.IsExpanded;
        }
        _storage.Save(_appData);
    }

    // ── 드래그 앤 드롭 ───────────────────────────────────────────────
    private void TreeView_ItemDrag(object? sender, ItemDragEventArgs e)
    {
        if (e.Item is not TreeNode { Tag: NodeTag { Type: NodeType.Session } } node) return;
        DoDragDrop(node, DragDropEffects.Move);
    }

    private void TreeView_DragEnter(object? sender, DragEventArgs e)
    {
        e.Effect = e.Data?.GetDataPresent(typeof(TreeNode)) == true
            ? DragDropEffects.Move
            : DragDropEffects.None;
    }

    private void TreeView_DragOver(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(typeof(TreeNode)) != true) { e.Effect = DragDropEffects.None; return; }

        var pt     = _treeView.PointToClient(new Point(e.X, e.Y));
        var target = _treeView.GetNodeAt(pt);

        // 이전 하이라이트 제거
        if (_dragTarget is not null && _dragTarget != target)
        {
            _dragTarget.BackColor = Color.Empty;
            _dragTarget.ForeColor = Color.Empty;
        }

        var isValidTarget = target?.Tag is NodeTag { Type: NodeType.Group or NodeType.Ungrouped };
        if (isValidTarget && target is not null)
        {
            target.BackColor = SystemColors.Highlight;
            target.ForeColor = SystemColors.HighlightText;
            _dragTarget = target;
            e.Effect    = DragDropEffects.Move;
        }
        else
        {
            _dragTarget = null;
            e.Effect    = DragDropEffects.None;
        }
    }

    private void TreeView_DragLeave(object? sender, EventArgs e)
    {
        if (_dragTarget is null) return;
        _dragTarget.BackColor = Color.Empty;
        _dragTarget.ForeColor = Color.Empty;
        _dragTarget = null;
    }

    private void TreeView_DragDrop(object? sender, DragEventArgs e)
    {
        // 하이라이트 초기화
        if (_dragTarget is not null)
        {
            _dragTarget.BackColor = Color.Empty;
            _dragTarget.ForeColor = Color.Empty;
            _dragTarget = null;
        }

        if (e.Data?.GetData(typeof(TreeNode)) is not TreeNode draggedNode) return;
        if (draggedNode.Tag is not NodeTag { Type: NodeType.Session } srcTag) return;

        var pt         = _treeView.PointToClient(new Point(e.X, e.Y));
        var targetNode = _treeView.GetNodeAt(pt);
        if (targetNode is null) return;

        var regName = (string)srcTag.Data!;

        if (targetNode.Tag is NodeTag { Type: NodeType.Group } dstTag)
            MoveSessionToGroup(regName, (Guid)dstTag.Data!);
        else if (targetNode.Tag is NodeTag { Type: NodeType.Ungrouped })
            RemoveSessionFromGroup(regName);
    }

    // ── 우클릭 메뉴 ──────────────────────────────────────────────────
    private void ShowContextMenu(TreeNode node, Point location)
    {
        if (node.Tag is not NodeTag tag) return;

        var menu = new ContextMenuStrip();

        if (tag.Type == NodeType.Group)
        {
            var group = _appData.Groups.First(g => g.Id.Equals(tag.Data));
            menu.Items.Add("이름 변경", null, (_, _) => RenameGroup(group));
            menu.Items.Add("그룹 삭제", null, (_, _) => DeleteGroup(group));
        }
        else if (tag.Type == NodeType.Session)
        {
            var regName = (string)tag.Data!;
            var session = _allSessions.FirstOrDefault(s => s.RegistryName == regName);

            menu.Items.Add("PuTTY로 열기", null, (_, _) =>
            {
                if (session is not null && !_launcher.Launch(session.DisplayName))
                    ShowPuttyNotFound();
            });

            if (_appData.Groups.Count > 0)
            {
                menu.Items.Add(new ToolStripSeparator());
                var addSub = new ToolStripMenuItem("그룹에 추가");
                foreach (var g in _appData.Groups)
                {
                    var captured = g;
                    addSub.DropDownItems.Add(g.Name, null,
                        (_, _) => MoveSessionToGroup(regName, captured.Id));
                }
                menu.Items.Add(addSub);
            }

            var inGroup = _appData.Groups.Any(g => g.SessionNames.Contains(regName));
            if (inGroup)
                menu.Items.Add("미분류로 이동", null, (_, _) => RemoveSessionFromGroup(regName));
        }

        if (menu.Items.Count > 0)
            menu.Show(_treeView, location);
    }

    // ── 그룹 조작 ────────────────────────────────────────────────────
    private void RenameGroup(SessionGroup group)
    {
        using var dlg = new GroupEditDialog("새 이름:", group.Name);
        if (dlg.ShowDialog() != DialogResult.OK || string.IsNullOrWhiteSpace(dlg.GroupName)) return;
        group.Name = dlg.GroupName;
        SaveAndRefresh();
    }

    private void DeleteGroup(SessionGroup group)
    {
        var res = MessageBox.Show(
            $"'{group.Name}' 그룹을 삭제합니다.\n세션은 미분류로 이동됩니다.",
            "그룹 삭제", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
        if (res != DialogResult.OK) return;

        _appData.Groups.Remove(group);
        SaveAndRefresh();
    }

    private void MoveSessionToGroup(string registryName, Guid groupId)
    {
        foreach (var g in _appData.Groups)
            g.SessionNames.Remove(registryName);

        var target = _appData.Groups.FirstOrDefault(g => g.Id == groupId);
        target?.SessionNames.Add(registryName);
        SaveAndRefresh();
    }

    private void RemoveSessionFromGroup(string registryName)
    {
        foreach (var g in _appData.Groups)
            g.SessionNames.Remove(registryName);
        SaveAndRefresh();
    }

    // ── 유틸 ─────────────────────────────────────────────────────────
    private void SaveAndRefresh()
    {
        _storage.Save(_appData);
        RefreshTree();
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        var groupedCount = _appData.Groups.Sum(g => g.SessionNames.Count);
        _statusLabel.Text = $"세션 {_allSessions.Count}개 | 그룹 {_appData.Groups.Count}개 | 분류됨 {groupedCount}개";
    }

    private void ShowPuttyNotFound() =>
        MessageBox.Show("PuTTY(putty.exe)를 찾을 수 없습니다.\nPuTTY가 설치되어 있는지 확인해주세요.",
            "PuTTY 없음", MessageBoxButtons.OK, MessageBoxIcon.Warning);
}

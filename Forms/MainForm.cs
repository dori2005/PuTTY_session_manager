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

    private enum NodeType { Group, Session, Ungrouped }
    private record NodeTag(NodeType Type, object? Data);

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

        var stream = typeof(MainForm).Assembly
            .GetManifestResourceStream("PuttySessionManager.Resources.app.ico");
        if (stream is not null) Icon = new Icon(stream);
    }

    private static Bitmap DrawGroupIcon()
    {
        var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var fill = new SolidBrush(Color.FromArgb(255, 230, 165, 30));
        g.FillRectangle(fill, 0, 4, 16, 10);
        using var tab = new SolidBrush(Color.FromArgb(255, 250, 190, 60));
        g.FillRectangle(tab, 0, 2, 7, 3);
        using var pen = new Pen(Color.FromArgb(180, 160, 100, 0), 1);
        g.DrawRectangle(pen, 0, 4, 15, 9);
        return bmp;
    }

    private static Bitmap DrawSessionIcon()
    {
        var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var bg = new SolidBrush(Color.FromArgb(255, 20, 20, 20));
        g.FillRectangle(bg, 0, 0, 16, 16);
        using var pen = new Pen(Color.FromArgb(255, 0, 180, 0), 1);
        g.DrawRectangle(pen, 0, 0, 15, 15);
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

        foreach (var group in _appData.Groups)
        {
            var node = BuildGroupNode(group);
            _treeView.Nodes.Add(node);
            if (group.IsExpanded) node.Expand();
        }

        // 어떤 그룹(하위 포함)에도 없는 세션 → 미분류
        var grouped   = new HashSet<string>();
        CollectAllSessionNames(_appData.Groups, grouped);
        var ungrouped = _allSessions.Where(s => !grouped.Contains(s.RegistryName)).ToList();

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

        // 하위 그룹 먼저 (재귀)
        foreach (var child in group.Children)
        {
            var childNode = BuildGroupNode(child);
            node.Nodes.Add(childNode);
            if (child.IsExpanded) childNode.Expand();
        }

        // 세션
        foreach (var s in valid)
            node.Nodes.Add(BuildSessionNode(s));

        return node;
    }

    private static TreeNode BuildSessionNode(PuttySession s) =>
        new(s.DisplayName)
        {
            Tag              = new NodeTag(NodeType.Session, s.RegistryName),
            ImageKey         = "session",
            SelectedImageKey = "session"
        };

    // ── 재귀 헬퍼 ────────────────────────────────────────────────────
    private static SessionGroup? FindGroup(IEnumerable<SessionGroup> groups, Guid id)
    {
        foreach (var g in groups)
        {
            if (g.Id == id) return g;
            var found = FindGroup(g.Children, id);
            if (found is not null) return found;
        }
        return null;
    }

    // group을 담고 있는 리스트를 반환 (루트 or 부모의 Children)
    private static List<SessionGroup>? FindContainer(List<SessionGroup> list, Guid id)
    {
        foreach (var g in list)
        {
            if (g.Id == id) return list;
            var found = FindContainer(g.Children, id);
            if (found is not null) return found;
        }
        return null;
    }

    private static void CollectAllSessionNames(IEnumerable<SessionGroup> groups, HashSet<string> result)
    {
        foreach (var g in groups)
        {
            result.UnionWith(g.SessionNames);
            CollectAllSessionNames(g.Children, result);
        }
    }

    private static void RemoveSessionFromAll(IEnumerable<SessionGroup> groups, string name)
    {
        foreach (var g in groups)
        {
            g.SessionNames.Remove(name);
            RemoveSessionFromAll(g.Children, name);
        }
    }

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
        if (!_launcher.Launch(session.DisplayName)) ShowPuttyNotFound();
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
            var group = FindGroup(_appData.Groups, (Guid)tag.Data!);
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

        if (_dragTarget is not null && _dragTarget != target)
        {
            _dragTarget.BackColor = Color.Empty;
            _dragTarget.ForeColor = Color.Empty;
        }

        if (target?.Tag is NodeTag { Type: NodeType.Group or NodeType.Ungrouped })
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
            var group = FindGroup(_appData.Groups, (Guid)tag.Data!)!;
            menu.Items.Add("새 하위 그룹", null, (_, _) => NewSubGroup(group));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("이름 변경",    null, (_, _) => RenameGroup(group));
            menu.Items.Add("그룹 삭제",    null, (_, _) => DeleteGroup(group));
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

            // "그룹에 추가" 서브메뉴 (모든 그룹 재귀 포함)
            var allGroups = new List<SessionGroup>();
            CollectAllGroups(_appData.Groups, allGroups);

            if (allGroups.Count > 0)
            {
                menu.Items.Add(new ToolStripSeparator());
                var addSub = new ToolStripMenuItem("그룹에 추가");
                BuildGroupMenuItems(addSub.DropDownItems, _appData.Groups, regName);
                menu.Items.Add(addSub);
            }

            var inGroup = IsInAnyGroup(_appData.Groups, regName);
            if (inGroup)
                menu.Items.Add("미분류로 이동", null, (_, _) => RemoveSessionFromGroup(regName));
        }

        if (menu.Items.Count > 0)
            menu.Show(_treeView, location);
    }

    // 그룹 메뉴 항목을 재귀적으로 구성
    private void BuildGroupMenuItems(ToolStripItemCollection items, List<SessionGroup> groups, string regName)
    {
        foreach (var g in groups)
        {
            if (g.Children.Count > 0)
            {
                var sub = new ToolStripMenuItem(g.Name);
                var captured = g;
                sub.DropDownItems.Add(g.Name + " (여기에 추가)", null,
                    (_, _) => MoveSessionToGroup(regName, captured.Id));
                sub.DropDownItems.Add(new ToolStripSeparator());
                BuildGroupMenuItems(sub.DropDownItems, g.Children, regName);
                items.Add(sub);
            }
            else
            {
                var captured = g;
                items.Add(g.Name, null, (_, _) => MoveSessionToGroup(regName, captured.Id));
            }
        }
    }

    private static void CollectAllGroups(IEnumerable<SessionGroup> groups, List<SessionGroup> result)
    {
        foreach (var g in groups)
        {
            result.Add(g);
            CollectAllGroups(g.Children, result);
        }
    }

    private static bool IsInAnyGroup(IEnumerable<SessionGroup> groups, string regName)
    {
        foreach (var g in groups)
        {
            if (g.SessionNames.Contains(regName)) return true;
            if (IsInAnyGroup(g.Children, regName)) return true;
        }
        return false;
    }

    // ── 그룹 조작 ────────────────────────────────────────────────────
    private void NewSubGroup(SessionGroup parent)
    {
        using var dlg = new GroupEditDialog("하위 그룹 이름:");
        if (dlg.ShowDialog() != DialogResult.OK || string.IsNullOrWhiteSpace(dlg.GroupName)) return;
        parent.Children.Add(new SessionGroup { Name = dlg.GroupName });
        SaveAndRefresh();
    }

    private void RenameGroup(SessionGroup group)
    {
        using var dlg = new GroupEditDialog("새 이름:", group.Name);
        if (dlg.ShowDialog() != DialogResult.OK || string.IsNullOrWhiteSpace(dlg.GroupName)) return;
        group.Name = dlg.GroupName;
        SaveAndRefresh();
    }

    private void DeleteGroup(SessionGroup group)
    {
        var hasChildren = group.Children.Count > 0 || group.SessionNames.Count > 0;
        var msg = hasChildren
            ? $"'{group.Name}' 그룹을 삭제합니다.\n하위 그룹은 상위로 이동되고, 세션은 미분류로 이동됩니다."
            : $"'{group.Name}' 그룹을 삭제합니다.";

        if (MessageBox.Show(msg, "그룹 삭제", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
            return;

        var container = FindContainer(_appData.Groups, group.Id) ?? _appData.Groups;
        var idx = container.IndexOf(group);
        container.Remove(group);

        // 자식 그룹을 같은 레벨로 승격 (삭제된 자리에 삽입)
        for (var i = 0; i < group.Children.Count; i++)
            container.Insert(idx + i, group.Children[i]);

        SaveAndRefresh();
    }

    private void MoveSessionToGroup(string registryName, Guid groupId)
    {
        RemoveSessionFromAll(_appData.Groups, registryName);
        FindGroup(_appData.Groups, groupId)?.SessionNames.Add(registryName);
        SaveAndRefresh();
    }

    private void RemoveSessionFromGroup(string registryName)
    {
        RemoveSessionFromAll(_appData.Groups, registryName);
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
        var grouped = new HashSet<string>();
        CollectAllSessionNames(_appData.Groups, grouped);
        var allGroups = new List<SessionGroup>();
        CollectAllGroups(_appData.Groups, allGroups);
        _statusLabel.Text = $"세션 {_allSessions.Count}개 | 그룹 {allGroups.Count}개 | 분류됨 {grouped.Count}개";
    }

    private void ShowPuttyNotFound() =>
        MessageBox.Show("PuTTY(putty.exe)를 찾을 수 없습니다.\nPuTTY가 설치되어 있는지 확인해주세요.",
            "PuTTY 없음", MessageBoxButtons.OK, MessageBoxIcon.Warning);
}

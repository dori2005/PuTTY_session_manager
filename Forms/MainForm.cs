using PuttySessionManager.Models;
using PuttySessionManager.Services;

namespace PuttySessionManager.Forms;

public partial class MainForm : Form
{
    private readonly GroupStorageService _storage  = new();
    private readonly PuttyLaunchService  _launcher = new();
    private readonly WinScpLaunchService _winScp   = new();

    private AppData            _appData     = new();
    private List<PuttySession> _allSessions = new();

    private enum NodeType { Group, Session, Ungrouped }
    private record NodeTag(NodeType Type, object? Data);

    // ── 드래그 앤 드롭 (세션 이동) ──────────────────────────────────
    private TreeNode? _dragTarget;

    // ── 다중 선택 ────────────────────────────────────────────────────
    private readonly List<TreeNode> _selectedNodes = new();

    // ── 고무줄 선택 ──────────────────────────────────────────────────
    private bool  _isRubberBanding;
    private Point _rbScreenStart;       // 시작점 (TreeView 클라이언트 좌표)

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
        using var bg   = new SolidBrush(Color.FromArgb(255, 20, 20, 20));
        g.FillRectangle(bg, 0, 0, 16, 16);
        using var pen  = new Pen(Color.FromArgb(255, 0, 180, 0), 1);
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

        // 그룹 먼저
        foreach (var group in _appData.Groups)
        {
            var node = BuildGroupNode(group);
            _treeView.Nodes.Add(node);
            if (group.IsExpanded) node.Expand();
        }

        // 미분류 세션을 최상위에 바로 표시
        var grouped = new HashSet<string>();
        CollectAllSessionNames(_appData.Groups, grouped);
        foreach (var s in _allSessions.Where(s => !grouped.Contains(s.RegistryName)))
            _treeView.Nodes.Add(BuildSessionNode(s));

        _treeView.EndUpdate();
        _selectedNodes.Clear();
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
        foreach (var child in group.Children)
        {
            var childNode = BuildGroupNode(child);
            node.Nodes.Add(childNode);
            if (child.IsExpanded) childNode.Expand();
        }
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

    private static void CollectAllGroups(IEnumerable<SessionGroup> groups, List<SessionGroup> result)
    {
        foreach (var g in groups) { result.Add(g); CollectAllGroups(g.Children, result); }
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

    // ── 다중 선택 관리 ───────────────────────────────────────────────
    private void SetSelected(TreeNode node, bool selected)
    {
        if (selected)
        {
            if (!_selectedNodes.Contains(node))
            {
                _selectedNodes.Add(node);
                node.BackColor = SystemColors.Highlight;
                node.ForeColor = SystemColors.HighlightText;
            }
        }
        else
        {
            _selectedNodes.Remove(node);
            node.BackColor = Color.Empty;
            node.ForeColor = Color.Empty;
        }
    }

    private void ToggleSelect(TreeNode node) =>
        SetSelected(node, !_selectedNodes.Contains(node));

    private void ClearSelection()
    {
        foreach (var n in _selectedNodes.ToList())
        {
            n.BackColor = Color.Empty;
            n.ForeColor = Color.Empty;
        }
        _selectedNodes.Clear();
    }

    private void SelectNodesInRect(TreeNodeCollection nodes, Rectangle clientRect)
    {
        int imgW = (_treeView.ImageList?.ImageSize.Width ?? 16) + 2;
        foreach (TreeNode node in nodes)
        {
            // 세션 + 그룹 선택 가능 (미분류는 제외)
            if (node.Tag is NodeTag { Type: NodeType.Session or NodeType.Group })
            {
                var b = node.Bounds;
                var hit = new Rectangle(b.Left - imgW, b.Top, b.Width + imgW, b.Height);
                if (clientRect.IntersectsWith(hit))
                    SetSelected(node, true);
            }
            SelectNodesInRect(node.Nodes, clientRect);
        }
    }

    // ── 고무줄 선택 ──────────────────────────────────────────────────
    /// <summary>노드의 아이콘 + 텍스트 박스 영역을 정확히 클릭했는지 판정.</summary>
    private bool IsClickInsideNodeBox(TreeNode node, Point clientPt)
    {
        // TreeView.GetNodeAt은 Y만 본다. 아이콘 + 텍스트 영역과 X, Y 모두 교차해야 진짜 클릭.
        var b = node.Bounds;
        int imgW = (_treeView.ImageList?.ImageSize.Width ?? 16) + 2; // 아이콘 폭 + 갭
        return clientPt.X >= b.Left - imgW && clientPt.X <= b.Right
            && clientPt.Y >= b.Top         && clientPt.Y <= b.Bottom;
    }

    // 고무줄 사각형 (TreeView 클라이언트 좌표). null이면 비표시.
    private Rectangle? _rbClientRect;

    private void TreeView_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;

        var node = _treeView.GetNodeAt(e.Location);
        if (node is not null && !IsClickInsideNodeBox(node, e.Location))
            node = null;

        if (node is not null)
        {
            if (!_selectedNodes.Contains(node)) { ClearSelection(); UpdateStatus(); }
        }
        else
        {
            // 빈 공간: 고무줄 시작 (클라이언트 좌표 사용)
            ClearSelection();
            _treeView.SelectedNode = null;          // 단일 선택 표시도 해제
            _isRubberBanding = true;
            _rbScreenStart   = e.Location;
            _rbClientRect    = null;
        }
    }

    private void TreeView_MouseMove(object? sender, MouseEventArgs e)
    {
        if (!_isRubberBanding) return;

        var cur = e.Location;
        var newRect = Rectangle.FromLTRB(
            Math.Min(_rbScreenStart.X, cur.X), Math.Min(_rbScreenStart.Y, cur.Y),
            Math.Max(_rbScreenStart.X, cur.X), Math.Max(_rbScreenStart.Y, cur.Y));

        // 이전 + 새 영역만 invalidate (전체 repaint X → 깜빡임 X)
        var prev = _rbClientRect;
        _rbClientRect = newRect;
        if (prev is { } p) _treeView.Invalidate(Rectangle.Inflate(p, 2, 2));
        _treeView.Invalidate(Rectangle.Inflate(newRect, 2, 2));

        // 선택 갱신
        foreach (var n in _selectedNodes.ToList())
        { n.BackColor = Color.Empty; n.ForeColor = Color.Empty; }
        _selectedNodes.Clear();
        SelectNodesInRect(_treeView.Nodes, newRect);
        UpdateStatus();
    }

    private void TreeView_MouseUp(object? sender, MouseEventArgs e)
    {
        if (!_isRubberBanding) return;
        var prev = _rbClientRect;
        _rbClientRect    = null;
        _isRubberBanding = false;
        if (prev is { } p) _treeView.Invalidate(Rectangle.Inflate(p, 2, 2));
        UpdateStatus();
    }

    /// <summary>TreeView의 WM_PAINT 이후 고무줄 사각형을 위에 그림. 깜빡임 없음.</summary>
    private void TreeView_PaintRubberBand(Graphics g)
    {
        if (_rbClientRect is not { } r || r.Width < 1 || r.Height < 1) return;
        using var fill = new SolidBrush(Color.FromArgb(40, 0, 120, 215));
        g.FillRectangle(fill, r);
        using var pen = new Pen(Color.FromArgb(255, 0, 120, 215), 1);
        g.DrawRectangle(pen, r.X, r.Y, r.Width - 1, r.Height - 1);
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
        if (e.KeyCode == Keys.Escape) { ClearSelection(); UpdateStatus(); }
    }

    private void TreeView_NodeMouseDoubleClick(object? sender, TreeNodeMouseClickEventArgs e)
    {
        if (e.Node?.Tag is not NodeTag { Type: NodeType.Session } tag) return;
        LaunchPutty((string)tag.Data!);
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
            _storage.Save(_appData);
        }
    }

    // ── 드래그 앤 드롭 (세션/그룹 이동) ─────────────────────────────
    private void TreeView_ItemDrag(object? sender, ItemDragEventArgs e)
    {
        if (e.Item is not TreeNode dragNode) return;
        // 세션 + 그룹만 드래그 (미분류 X)
        if (dragNode.Tag is not NodeTag { Type: NodeType.Session or NodeType.Group }) return;

        var nodes = _selectedNodes.Count > 1 && _selectedNodes.Contains(dragNode)
            ? _selectedNodes.ToList()
            : new List<TreeNode> { dragNode };

        DoDragDrop(nodes, DragDropEffects.Move);
    }

    private void TreeView_DragEnter(object? sender, DragEventArgs e)
    {
        e.Effect = e.Data?.GetDataPresent(typeof(List<TreeNode>)) == true
            ? DragDropEffects.Move : DragDropEffects.None;
    }

    private void TreeView_DragOver(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(typeof(List<TreeNode>)) != true)
        { e.Effect = DragDropEffects.None; return; }

        var pt     = _treeView.PointToClient(new Point(e.X, e.Y));
        var target = _treeView.GetNodeAt(pt);

        if (_dragTarget is not null && _dragTarget != target)
        { _dragTarget.BackColor = Color.Empty; _dragTarget.ForeColor = Color.Empty; }

        if (target?.Tag is NodeTag { Type: NodeType.Group })
        {
            target.BackColor = SystemColors.Highlight;
            target.ForeColor = SystemColors.HighlightText;
            _dragTarget = target;
            e.Effect    = DragDropEffects.Move;
        }
        else
        {
            // 빈 공간 또는 세션 위 = 루트(미분류)로 이동
            _dragTarget = null;
            e.Effect    = DragDropEffects.Move;
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
        { _dragTarget.BackColor = Color.Empty; _dragTarget.ForeColor = Color.Empty; _dragTarget = null; }

        if (e.Data?.GetData(typeof(List<TreeNode>)) is not List<TreeNode> draggedNodes) return;

        var pt         = _treeView.PointToClient(new Point(e.X, e.Y));
        var targetNode = _treeView.GetNodeAt(pt);

        var regNames = draggedNodes
            .Where(n => n.Tag is NodeTag { Type: NodeType.Session })
            .Select(n => (string)((NodeTag)n.Tag!).Data!)
            .ToList();

        var groupIds = draggedNodes
            .Where(n => n.Tag is NodeTag { Type: NodeType.Group })
            .Select(n => (Guid)((NodeTag)n.Tag!).Data!)
            .ToList();

        if (targetNode?.Tag is NodeTag { Type: NodeType.Group } dstTag)
        {
            var dstId = (Guid)dstTag.Data!;
            foreach (var r in regNames) MoveSessionToGroup(r, dstId, refresh: false);
            foreach (var gid in groupIds) MoveGroupToGroup(gid, dstId);
        }
        else
        {
            // 빈 공간 또는 세션 위에 드롭 = 루트(미분류)로 이동
            foreach (var r in regNames) RemoveSessionFromAll(_appData.Groups, r);
            foreach (var gid in groupIds) MoveGroupToRoot(gid);
        }

        SaveAndRefresh();
    }

    /// <summary>그룹을 다른 그룹의 하위로 이동. 자기 자신/자손에게 드롭은 무시.</summary>
    private void MoveGroupToGroup(Guid groupId, Guid destId)
    {
        if (groupId == destId) return;

        var moving = FindGroup(_appData.Groups, groupId);
        if (moving is null) return;

        // 자기 자손에게 드롭 → cycle. 무시.
        if (FindGroup(moving.Children, destId) is not null) return;

        var dest = FindGroup(_appData.Groups, destId);
        if (dest is null) return;

        // 이미 destId의 직계 자식이면 변경 없음
        if (dest.Children.Contains(moving)) return;

        // 원래 위치에서 제거
        var src = FindContainer(_appData.Groups, groupId);
        src?.Remove(moving);

        // 새 부모로 이동
        dest.Children.Add(moving);
    }

    /// <summary>그룹을 최상위(루트)로 이동.</summary>
    private void MoveGroupToRoot(Guid groupId)
    {
        var moving = FindGroup(_appData.Groups, groupId);
        if (moving is null) return;
        if (_appData.Groups.Contains(moving)) return;   // 이미 루트

        var src = FindContainer(_appData.Groups, groupId);
        src?.Remove(moving);
        _appData.Groups.Add(moving);
    }

    // ── 우클릭 메뉴 ──────────────────────────────────────────────────
    private void ShowContextMenu(TreeNode node, Point location)
    {
        if (node.Tag is not NodeTag tag) return;
        var menu = new ContextMenuStrip();

        // 다중 선택 상태이고 우클릭 노드가 세션이면 다중 메뉴
        if (_selectedNodes.Count > 1
            && tag.Type == NodeType.Session
            && _selectedNodes.Contains(node))
        {
            ShowMultiSelectMenu(menu);
        }
        else if (tag.Type == NodeType.Group)
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

            menu.Items.Add("PuTTY로 열기",  null, (_, _) => LaunchPutty(regName));
            menu.Items.Add("WinSCP로 열기", null, (_, _) => LaunchWinScp(regName));

            menu.Items.Add(new ToolStripSeparator());

            var allGroups = new List<SessionGroup>();
            CollectAllGroups(_appData.Groups, allGroups);
            if (allGroups.Count > 0)
            {
                var addSub = new ToolStripMenuItem("그룹에 추가");
                BuildGroupMenuItems(addSub.DropDownItems, _appData.Groups, regName);
                menu.Items.Add(addSub);
            }

            if (IsInAnyGroup(_appData.Groups, regName))
                menu.Items.Add("그룹에서 제외", null, (_, _) => RemoveSessionFromGroup(regName));
        }

        if (menu.Items.Count > 0) menu.Show(_treeView, location);
    }

    private void ShowMultiSelectMenu(ContextMenuStrip menu)
    {
        var count = _selectedNodes.Count;
        menu.Items.Add($"PuTTY로 {count}개 열기", null, (_, _) =>
        {
            foreach (var n in _selectedNodes.ToList())
                if (n.Tag is NodeTag { Type: NodeType.Session } t)
                    LaunchPutty((string)t.Data!);
        });

        menu.Items.Add($"WinSCP로 {count}개 열기", null, (_, _) =>
        {
            foreach (var n in _selectedNodes.ToList())
                if (n.Tag is NodeTag { Type: NodeType.Session } t)
                    LaunchWinScp((string)t.Data!);
        });

        if (_appData.Groups.Count > 0)
        {
            menu.Items.Add(new ToolStripSeparator());
            var addSub = new ToolStripMenuItem("선택 항목 그룹에 추가");
            foreach (var g in _appData.Groups)
            {
                var captured = g;
                addSub.DropDownItems.Add(g.Name, null, (_, _) =>
                {
                    foreach (var n in _selectedNodes.ToList())
                        if (n.Tag is NodeTag { Type: NodeType.Session } t)
                            MoveSessionToGroup((string)t.Data!, captured.Id, refresh: false);
                    SaveAndRefresh();
                });
            }
            menu.Items.Add(addSub);
        }

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("선택 해제", null, (_, _) => { ClearSelection(); UpdateStatus(); });
    }

    private void BuildGroupMenuItems(ToolStripItemCollection items, List<SessionGroup> groups, string regName)
    {
        foreach (var g in groups)
        {
            if (g.Children.Count > 0)
            {
                var sub = new ToolStripMenuItem(g.Name);
                var cap = g;
                sub.DropDownItems.Add($"{g.Name} (여기에 추가)", null,
                    (_, _) => MoveSessionToGroup(regName, cap.Id));
                sub.DropDownItems.Add(new ToolStripSeparator());
                BuildGroupMenuItems(sub.DropDownItems, g.Children, regName);
                items.Add(sub);
            }
            else
            {
                var cap = g;
                items.Add(g.Name, null, (_, _) => MoveSessionToGroup(regName, cap.Id));
            }
        }
    }

    // ── 실행 ─────────────────────────────────────────────────────────
    private void LaunchPutty(string registryName)
    {
        var session = _allSessions.FirstOrDefault(s => s.RegistryName == registryName);
        if (session is null) return;
        if (!_launcher.Launch(session.DisplayName)) ShowNotFound("PuTTY");
    }

    private void LaunchWinScp(string registryName)
    {
        var session = _allSessions.FirstOrDefault(s => s.RegistryName == registryName);
        if (session is null) return;
        var (host, user) = RegistryService.GetSessionDetails(registryName);
        if (!_winScp.Launch(session.DisplayName, host, user)) ShowNotFound("WinSCP");
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
            ? $"'{group.Name}' 그룹을 삭제합니다.\n하위 그룹과 세션은 상위로 이동됩니다."
            : $"'{group.Name}' 그룹을 삭제합니다.";

        if (MessageBox.Show(msg, "그룹 삭제", MessageBoxButtons.OKCancel, MessageBoxIcon.Question)
            != DialogResult.OK) return;

        var container = FindContainer(_appData.Groups, group.Id) ?? _appData.Groups;
        var idx = container.IndexOf(group);
        container.Remove(group);
        for (var i = 0; i < group.Children.Count; i++)
            container.Insert(idx + i, group.Children[i]);

        SaveAndRefresh();
    }

    private void MoveSessionToGroup(string registryName, Guid groupId, bool refresh = true)
    {
        RemoveSessionFromAll(_appData.Groups, registryName);
        FindGroup(_appData.Groups, groupId)?.SessionNames.Add(registryName);
        if (refresh) SaveAndRefresh();
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

        var selText = _selectedNodes.Count > 0 ? $" | 선택 {_selectedNodes.Count}개" : "";
        _statusLabel.Text =
            $"세션 {_allSessions.Count}개 | 그룹 {allGroups.Count}개 | 분류됨 {grouped.Count}개{selText}";
    }

    private void ShowNotFound(string app) =>
        MessageBox.Show($"{app}를 찾을 수 없습니다.\n{app}가 설치되어 있는지 확인해주세요.",
            $"{app} 없음", MessageBoxButtons.OK, MessageBoxIcon.Warning);
}

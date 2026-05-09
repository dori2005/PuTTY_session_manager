using System.ComponentModel;
using System.Runtime.InteropServices;

namespace PuttySessionManager.Forms;

/// <summary>
/// WM_PAINT 후에 부모가 등록한 콜백을 호출해 고무줄 사각형 등을 위에 그릴 수 있게 하는 TreeView.
/// 별도 오버레이 윈도우를 쓰지 않으므로 z-order 변경에 따른 깜빡임이 없다.
/// </summary>
internal sealed class RubberBandTreeView : TreeView
{
    private const int WM_PAINT = 0x000F;

    /// <summary>WM_PAINT 직후 호출됨. TreeView의 클라이언트 좌표계에서 그리기.</summary>
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Action<Graphics>? PostPaint { get; set; }

    public RubberBandTreeView()
    {
        // 더블 버퍼링 — TreeView는 native control이라 ControlStyles.DoubleBuffer로는 부족함.
        // 이 플래그는 TreeView 내부의 native double-buffering을 활성화.
        SetStyle(ControlStyles.OptimizedDoubleBuffer
               | ControlStyles.AllPaintingInWmPaint, true);
        UpdateStyles();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            // WS_EX_COMPOSITED — 자식 컨트롤 합성으로 깜빡임 추가 감소
            cp.ExStyle |= 0x02000000;
            return cp;
        }
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);

        if (m.Msg == WM_PAINT && PostPaint is { } cb)
        {
            using var g = Graphics.FromHwnd(Handle);
            cb(g);
        }
    }
}

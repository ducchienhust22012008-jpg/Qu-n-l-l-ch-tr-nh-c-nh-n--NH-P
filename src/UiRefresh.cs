using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Windows.Forms;

namespace Nhip {
    static class UiMenus {
        public static void AfterClick(Control owner, Action action) {
            if (!owner.IsDisposed && owner.IsHandleCreated) owner.BeginInvoke((Action)(() => { if (!owner.IsDisposed) action(); }));
        }
        public static void DisposeAfterEvent(ContextMenuStrip menu) {
            if (menu == null || menu.IsDisposed) return;
            var context = SynchronizationContext.Current;
            if (context == null) { menu.Dispose(); return; }
            context.Post(state => { if (!menu.IsDisposed) menu.Dispose(); }, null);
        }
    }
    sealed class RoundedButton : Button {
        public string NavigationGlyph;
        public bool DropDownIndicator;
        bool hovered,pressed;
        public RoundedButton() { DoubleBuffered=true; }
        protected override void OnMouseEnter(EventArgs e) { hovered=true;base.OnMouseEnter(e);Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { hovered=pressed=false;base.OnMouseLeave(e);Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { pressed=true;base.OnMouseDown(e);Invalidate(); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed=false;base.OnMouseUp(e);Invalidate(); }
        protected override void OnPaint(PaintEventArgs e) {
            if(Width<18 || Height<18)return;
            var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(Parent==null?Theme.Bg:Parent.BackColor);
            Color fill=!Enabled?Theme.Line:pressed?Theme.Tint(BackColor,.80):hovered?FlatAppearance.MouseOverBackColor:BackColor;
            var r=new Rectangle(0,0,Width-1,Height-1);
            using(var path=Theme.Rounded(r,8)) {
                using(var brush=new SolidBrush(fill))g.FillPath(brush,path);
                if(FlatAppearance.BorderSize>0)using(var pen=new Pen(FlatAppearance.BorderColor,FlatAppearance.BorderSize))g.DrawPath(pen,path);
            }
            int inset=Width<=52?2:8;
            var textBounds=new Rectangle(inset+Padding.Left,Padding.Top,Math.Max(1,Width-2*inset-Padding.Horizontal-(DropDownIndicator?20:0)),Math.Max(1,Height-Padding.Vertical));
            TextFormatFlags flags=TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix;
            flags|=TextAlign==ContentAlignment.MiddleLeft?TextFormatFlags.Left:TextAlign==ContentAlignment.MiddleRight?TextFormatFlags.Right:TextFormatFlags.HorizontalCenter;
            TextRenderer.DrawText(g,Text,Font,textBounds,Enabled?ForeColor:Theme.Muted,flags);
            if(DropDownIndicator)TextRenderer.DrawText(g,"⌄",Font,new Rectangle(Width-24,0,20,Height),Enabled?ForeColor:Theme.Muted,TextFormatFlags.VerticalCenter|TextFormatFlags.HorizontalCenter);
            if(!String.IsNullOrEmpty(NavigationGlyph)) {
                if(Tag is bool && (bool)Tag)using(var brush=new SolidBrush(Theme.Purple))g.FillRectangle(brush,0,10,3,Height-20);
                using(var font=new Font("Segoe MDL2 Assets",14))TextRenderer.DrawText(g,NavigationGlyph,font,new Rectangle(10,0,22,Height),ForeColor,TextFormatFlags.VerticalCenter|TextFormatFlags.HorizontalCenter);
            }
            if(Focused && ShowFocusCues)ControlPaint.DrawFocusRectangle(g,new Rectangle(4,4,Width-8,Height-8));
        }
        protected override void OnSizeChanged(EventArgs e) {
            base.OnSizeChanged(e);
            if (Width < 18 || Height < 18) return;
            using (var path = Theme.Rounded(new Rectangle(0,0,Width,Height),8)) {
                var previous = Region; Region = new Region(path); if(previous != null)previous.Dispose();
            }
        }
    }
    sealed class PillLabel : Label {
        public PillLabel() { AutoSize=false; DoubleBuffered=true; }
        protected override void OnPaintBackground(PaintEventArgs e) {
            if(Width<18 || Height<18){base.OnPaintBackground(e);return;}
            e.Graphics.Clear(Parent==null?Color.White:Parent.BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            using(var path=Theme.Rounded(new Rectangle(0,0,Width-1,Height-1),Math.Min(10,Height/2)))
            using(var brush=new SolidBrush(BackColor))e.Graphics.FillPath(brush,path);
        }
    }
}

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Nhip {
    sealed class PlanningProgress : Control {
        public int Completed, Count;
        public Color FillColor=Theme.Purple;
        public PlanningProgress() { DoubleBuffered = true; BackColor = Color.White; }
        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e); int width = Math.Max(1, Width - 42), y = Height / 2 - 2;
            double ratio = Count > 0 ? Math.Max(0, Math.Min(1, (double)Completed / Count)) : 0;
            using (var brush = new SolidBrush(Theme.Line)) e.Graphics.FillRectangle(brush, 0, y, width, 4);
            using (var brush = new SolidBrush(FillColor)) e.Graphics.FillRectangle(brush, 0, y, (int)(width * ratio), 4);
            using (var font = Theme.Font(8)) TextRenderer.DrawText(e.Graphics, (100 * ratio).ToString("0") + "%", font, new Rectangle(width + 4, 0, 38, Height), Theme.Muted, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
            AccessibleName = Completed + " / " + Count + " hoàn thành";
        }
    }
    sealed class TimelineEntry : Panel {
        readonly Control card;
        public readonly TaskItem Task;
        public bool First, Last;
        public TimelineEntry(TaskItem task, Control taskCard) {
            Task = task; card = taskCard; Height = 100; Margin = new Padding(0); BackColor = Theme.Bg; DoubleBuffered = true;
            Controls.Add(card); Resize += (s, e) => Arrange(); Arrange();
        }
        void Arrange() { card.SetBounds(96, 3, Math.Max(100, Width - 98), 88); }
        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e); var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; const int x = 79, y = 45;
            using (var line = new Pen(Color.FromArgb(228, 219, 252), 3)) g.DrawLine(line, x, First ? y : 0, x, Last ? y : Height);
            var node = new Rectangle(x - 10, y - 10, 20, 20);
            using (var brush = new SolidBrush(Task.Done ? Theme.Purple : Color.White)) g.FillEllipse(brush, node);
            using (var pen = new Pen(Theme.Purple, 2)) g.DrawEllipse(pen, node);
            if (Task.Done) using (var pen = new Pen(Color.White, 2)) g.DrawLines(pen, new[] { new Point(x - 5, y), new Point(x - 1, y + 4), new Point(x + 6, y - 4) });
            using (var font = new Font("Consolas", 11, FontStyle.Bold)) TextRenderer.DrawText(g, Task.Time, font, new Rectangle(0, y - 13, 64, 27), Theme.Ink, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
        }
    }
}

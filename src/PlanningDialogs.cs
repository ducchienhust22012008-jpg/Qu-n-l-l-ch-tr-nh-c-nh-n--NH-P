using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace Nhip {
    class TaskColorDialog : BrandedForm {
        public string ColorHex { get; private set; }
        public TaskColorDialog(string current) {
            Text = "Đổi màu công việc"; ClientSize = new Size(460, 270); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; StartPosition = FormStartPosition.CenterParent; BackColor = Theme.Bg; AutoScaleMode = AutoScaleMode.Dpi;
            var heading = Theme.Label("Chọn màu cho khối công việc", 17, Theme.Ink, true); heading.SetBounds(22, 12, 416, 44); Controls.Add(heading);
            string[] names = { "Tím", "Xanh dương", "Xanh lá", "Cam", "Hồng", "Đỏ", "Xanh ngọc", "Xám" };
            string[] colors = { "#6D52DD", "#2870BF", "#25834D", "#B87122", "#C14687", "#C84143", "#16877D", "#596477" };
            for (int i = 0; i < colors.Length; i++) {
                string hex = colors[i]; var b = Theme.Button(names[i]); b.BackColor = ColorTranslator.FromHtml(hex); b.ForeColor = Color.White; b.FlatAppearance.BorderSize = current == hex ? 3 : 0; b.FlatAppearance.BorderColor = Theme.Ink;
                b.SetBounds(22 + i % 4 * 105, 70 + i / 4 * 54, 98, 44); b.Click += (s, e) => Choose(hex); Controls.Add(b);
            }
            var automatic = Theme.Button("Theo nhóm"); automatic.SetBounds(22, 198, 126, 42); automatic.Click += (s, e) => Choose(null); Controls.Add(automatic);
            var custom = Theme.Button("Màu khác…"); custom.SetBounds(159, 198, 126, 42); custom.Click += (s, e) => {
                using (var picker = new ColorDialog { FullOpen = true, Color = String.IsNullOrEmpty(current) ? Theme.Purple : ColorTranslator.FromHtml(current) }) if (picker.ShowDialog(this) == DialogResult.OK) Choose("#" + picker.Color.R.ToString("X2") + picker.Color.G.ToString("X2") + picker.Color.B.ToString("X2"));
            }; Controls.Add(custom);
            var cancel = Theme.Button("Hủy"); cancel.SetBounds(296, 198, 140, 42); cancel.DialogResult = DialogResult.Cancel; CancelButton = cancel; Controls.Add(cancel);
        }
        void Choose(string hex) { ColorHex = hex; DialogResult = DialogResult.OK; Close(); }
        public void ChooseForTest() { Controls.OfType<Button>().First(b => b.Text == "Hồng").PerformClick(); }
    }
    static class Grids {
        public static DataGridView Create() {
            var grid = new DataGridView { BackgroundColor = Color.White, BorderStyle = BorderStyle.None, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, RowHeadersVisible = false, AllowUserToResizeRows = false, SelectionMode = DataGridViewSelectionMode.CellSelect, MultiSelect = false, Font = Theme.Font(10), GridColor = Theme.Line, EnableHeadersVisualStyles = false, ColumnHeadersHeight = 40, ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing };
            grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Theme.Sidebar, ForeColor = Theme.Ink, Font = Theme.Font(9, true), Padding = new Padding(6) };
            grid.DefaultCellStyle.Padding = new Padding(6, 3, 6, 3); grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(235, 230, 253); grid.DefaultCellStyle.SelectionForeColor = Theme.Ink;
            grid.CellBorderStyle=DataGridViewCellBorderStyle.SingleHorizontal;
            grid.RowTemplate.Height = 42;
            grid.DataError += (s, e) => { e.ThrowException = false; grid.Rows[e.RowIndex].ErrorText = "Hãy nhập số nguyên hợp lệ."; };
            return grid;
        }
        public static void Column(DataGridView grid, string name, string caption, int weight, bool readOnly = false) {
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = name, HeaderText = caption, FillWeight = weight, ReadOnly = readOnly, SortMode = DataGridViewColumnSortMode.NotSortable });
        }
    }
    class SettingsDialog : BrandedForm {
        NumericUpDown defaultReward = new NumericUpDown { Minimum = 0, Maximum = 600 };
        DataGridView grid = Grids.Create();
        public int DefaultReward { get; private set; }
        public Dictionary<string, int> Rewards { get; private set; }
        public SettingsDialog(AppData data) {
            Text = "Cài đặt phút thưởng"; ClientSize = new Size(770, 620); MinimumSize = Size; StartPosition = FormStartPosition.CenterParent; BackColor = Theme.Bg; Font = Theme.Font(10); AutoScaleMode = AutoScaleMode.Dpi;
            var head = Theme.Label("Cài đặt phút thưởng", 23, Theme.Ink, true); head.SetBounds(24, 16, 700, 48); Controls.Add(head);
            var label = Theme.Label("Phút thưởng mặc định cho công việc mới", 11, Theme.Ink); label.SetBounds(26, 82, 460, 32); Controls.Add(label);
            defaultReward.SetBounds(570, 82, 170, 32); defaultReward.Value = data.Settings.DefaultReward; defaultReward.Font = Theme.Font(12); defaultReward.Anchor = AnchorStyles.Top | AnchorStyles.Right; Controls.Add(defaultReward);
            var caption = Theme.Label("ĐIỀU CHỈNH RIÊNG TỪNG CÔNG VIỆC", 9, Theme.Muted, true); caption.SetBounds(26, 138, 700, 32); Controls.Add(caption);
            grid.SetBounds(26, 179, 714, 300); grid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right; grid.AllowUserToAddRows = false; grid.AllowUserToDeleteRows = false;
            Grids.Column(grid, "title", "Công việc", 48, true); Grids.Column(grid, "day", "Ngày", 20, true); Grids.Column(grid, "status", "Trạng thái", 19, true); Grids.Column(grid, "reward", "Phút thưởng", 18);
            foreach (var t in data.Tasks.OrderBy(t => t.Day).ThenBy(t => t.Time)) { int row = grid.Rows.Add(t.Title, DateTime.ParseExact(t.Day, "yyyy-MM-dd", CultureInfo.InvariantCulture).ToString("dd/MM/yyyy"), t.Done ? "Đã xong" : "Chưa xong", t.Reward); grid.Rows[row].Tag = t.Id; }
            Controls.Add(grid);
            var hint = Theme.Label("Sửa trực tiếp cột Phút thưởng (0–600). Phần thưởng đã nhận được giữ nguyên.\nMức mới áp dụng khi hoàn thành lần tiếp theo; không cộng lại vào ví ngay khi lưu.", 9, Theme.Muted); hint.SetBounds(26, 491, 714, 54); hint.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom; Controls.Add(hint);
            var cancel = Theme.Button("Hủy"); cancel.SetBounds(480, 561, 100, 40); cancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right; cancel.DialogResult = DialogResult.Cancel; Controls.Add(cancel); CancelButton = cancel;
            var save = Theme.Button("Lưu cài đặt", true); save.SetBounds(592, 561, 148, 40); save.Anchor = AnchorStyles.Bottom | AnchorStyles.Right; save.Click += (s, e) => Save(); Controls.Add(save);
        }
        void Save() {
            grid.EndEdit(); var values = new Dictionary<string, int>();
            foreach (DataGridViewRow row in grid.Rows) {
                int value;
                if (!Int32.TryParse(Convert.ToString(row.Cells["reward"].Value), out value) || value < 0 || value > 600) { grid.CurrentCell = row.Cells["reward"]; MessageBox.Show(this, "Phút thưởng phải là số nguyên từ 0 đến 600.", "Kiểm tra phút thưởng"); return; }
                values[(string)row.Tag] = value;
            }
            DefaultReward = (int)defaultReward.Value; Rewards = values; DialogResult = DialogResult.OK; Close();
        }
        public void FillForTest(int minutes, int taskMinutes) { defaultReward.Value = minutes; if (grid.Rows.Count > 0) grid.Rows[0].Cells["reward"].Value = taskMinutes; Save(); }
    }
    class GoalsDialog : BrandedForm {
        DataGridView grid = Grids.Create(); string month;
        public List<MonthlyGoal> Goals { get; private set; }
        public GoalsDialog(AppData data, DateTime selectedMonth) {
            month = selectedMonth.ToString("yyyy-MM"); Text = "Mục tiêu tháng " + selectedMonth.ToString("MM/yyyy");
            ClientSize = new Size(770, 560); MinimumSize = Size; StartPosition = FormStartPosition.CenterParent; Font = Theme.Font(10); BackColor = Theme.Bg; AutoScaleMode = AutoScaleMode.Dpi;
            var head = Theme.Label(Text, 23, Theme.Ink, true); head.SetBounds(24, 16, 720, 52); Controls.Add(head);
            var hint = Theme.Label("Tick ô Xong để hoàn thành hoặc bỏ tick để mở lại mục tiêu.\nCác số liệu tiến độ được giữ để bạn theo dõi; không tự tick khi đổi số liệu.", 10, Theme.Muted); hint.SetBounds(26, 75, 718, 58); Controls.Add(hint);
            grid.SetBounds(26, 150, 718, 276); grid.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom; grid.AllowUserToAddRows = true; grid.AllowUserToDeleteRows = true;
            grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "done", HeaderText = "Xong", FillWeight = 12, SortMode = DataGridViewColumnSortMode.NotSortable });
            Grids.Column(grid, "title", "Mục tiêu", 42); Grids.Column(grid, "current", "Đã đạt", 15); Grids.Column(grid, "target", "Chỉ tiêu", 15); Grids.Column(grid, "unit", "Đơn vị", 17);
            ((DataGridViewTextBoxColumn)grid.Columns["title"]).MaxInputLength = 120; ((DataGridViewTextBoxColumn)grid.Columns["unit"]).MaxInputLength = 20;
            grid.DefaultValuesNeeded += (s, e) => { e.Row.Cells["done"].Value = false; e.Row.Cells["current"].Value = 0; e.Row.Cells["target"].Value = 1; e.Row.Cells["unit"].Value = "lần"; };
            foreach (var g in data.Goals.Where(g => g.Month == month)) { int index = grid.Rows.Add(Store.GoalIsDone(g), g.Title, g.Current, g.Target, g.Unit); grid.Rows[index].Tag = g.Id; }
            Controls.Add(grid);
            var remove = Theme.Button("Xóa dòng đang chọn"); remove.SetBounds(26, 440, 190, 34); remove.Anchor = AnchorStyles.Bottom | AnchorStyles.Left; remove.Click += (s, e) => { if (grid.CurrentRow != null && !grid.CurrentRow.IsNewRow) grid.Rows.Remove(grid.CurrentRow); }; Controls.Add(remove);
            var help = Theme.Label("Thêm mục tiêu ở dòng trống cuối bảng.", 9, Theme.Muted); help.SetBounds(230, 440, 490, 34); help.Anchor = AnchorStyles.Left | AnchorStyles.Bottom; Controls.Add(help);
            var cancel = Theme.Button("Hủy"); cancel.SetBounds(484, 498, 100, 40); cancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right; cancel.DialogResult = DialogResult.Cancel; Controls.Add(cancel); CancelButton = cancel;
            var save = Theme.Button("Lưu mục tiêu", true); save.SetBounds(596, 498, 148, 40); save.Anchor = AnchorStyles.Bottom | AnchorStyles.Right; save.Click += (s, e) => Save(); Controls.Add(save);
        }
        void Save() {
            grid.EndEdit(); var goals = new List<MonthlyGoal>();
            foreach (DataGridViewRow row in grid.Rows) {
                if (row.IsNewRow) continue;
                string name = Convert.ToString(row.Cells["title"].Value).Trim(); int current, target;
                if (name.Length == 0 || !Int32.TryParse(Convert.ToString(row.Cells["current"].Value), out current) || !Int32.TryParse(Convert.ToString(row.Cells["target"].Value), out target) || current < 0 || current > 1000000 || target < 1 || target > 1000000) { grid.CurrentCell = row.Cells["title"]; MessageBox.Show(this, "Mỗi dòng cần tên, tiến độ không âm và chỉ tiêu từ 1 đến 1.000.000.", "Kiểm tra mục tiêu"); return; }
                goals.Add(new MonthlyGoal { Id = row.Tag as string ?? Guid.NewGuid().ToString("N"), Month = month, Title = name, Current = current, Target = target, Unit = Convert.ToString(row.Cells["unit"].Value).Trim(), Done = Convert.ToBoolean(row.Cells["done"].Value ?? false) });
            }
            Goals = goals; DialogResult = DialogResult.OK; Close();
        }
        public void FillForTest() { grid.Rows.Add(false, "Đọc sách", 8, 20, "trang"); Save(); }
    }
}

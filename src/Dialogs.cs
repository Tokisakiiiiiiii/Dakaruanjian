using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DakaHelper
{
    /// <summary>新建 / 编辑习惯的对话框：名称 + 颜色。</summary>
    public class HabitDialog : Form
    {
        public static readonly string[] Palette = new string[]
        {
            "#2F6FED", "#7C4DFF", "#22A06B", "#E8894A",
            "#D64545", "#0EA5B7", "#D946A0", "#6B7280"
        };

        private readonly TextBox _nameBox;
        private readonly PaletteStrip _palette;

        public string HabitName
        {
            get { return _nameBox.Text.Trim(); }
        }

        public string HabitColor
        {
            get { return _palette.SelectedColor; }
        }

        public HabitDialog(string title, string initialName, string initialColor)
        {
            this.Text = title;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.BackColor = Color.White;
            this.Font = Theme.Base;
            this.ClientSize = new Size(384, 202);
            this.KeyPreview = true;

            Label nameLabel = new Label();
            nameLabel.Text = "习惯名称";
            nameLabel.SetBounds(22, 22, 68, 24);
            nameLabel.ForeColor = Theme.TextMuted;
            nameLabel.TextAlign = ContentAlignment.MiddleLeft;
            nameLabel.BackColor = Color.Transparent;

            _nameBox = new TextBox();
            _nameBox.SetBounds(94, 20, 268, 26);
            _nameBox.Font = Theme.Base;
            _nameBox.BorderStyle = BorderStyle.FixedSingle;
            _nameBox.Text = initialName == null ? "" : initialName;
            _nameBox.SelectAll();

            Label colorLabel = new Label();
            colorLabel.Text = "颜色标记";
            colorLabel.SetBounds(22, 72, 68, 24);
            colorLabel.ForeColor = Theme.TextMuted;
            colorLabel.TextAlign = ContentAlignment.MiddleLeft;
            colorLabel.BackColor = Color.Transparent;

            _palette = new PaletteStrip();
            _palette.SetBounds(94, 64, 268, 40);
            _palette.SelectColor(initialColor);

            ModernButton cancel = new ModernButton();
            cancel.Text = "取消";
            cancel.Accent = Theme.Hex("#E8EAEF");
            cancel.ForeColor = Theme.TextMain;
            cancel.SetBounds(268, 146, 94, 34);
            cancel.Click += delegate(object s, EventArgs e)
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            };

            ModernButton ok = new ModernButton();
            ok.Text = "确定";
            ok.Accent = Theme.Primary;
            ok.SetBounds(166, 146, 94, 34);
            ok.Click += delegate(object s, EventArgs e) { Confirm(); };

            this.Controls.Add(nameLabel);
            this.Controls.Add(_nameBox);
            this.Controls.Add(colorLabel);
            this.Controls.Add(_palette);
            this.Controls.Add(ok);
            this.Controls.Add(cancel);

            this.AcceptButton = null;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                Confirm();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        private void Confirm()
        {
            if (HabitName.Length == 0)
            {
                MessageBox.Show(this, "请先填写习惯名称。", "打卡助手",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                _nameBox.Focus();
                return;
            }
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }

    /// <summary>一排可点选的颜色色块。</summary>
    public class PaletteStrip : Control
    {
        private int _selectedIndex;
        private int _hoverIndex = -1;

        public event EventHandler SelectionChanged;

        public string SelectedColor
        {
            get
            {
                if (_selectedIndex < 0 || _selectedIndex >= HabitDialog.Palette.Length)
                {
                    return HabitDialog.Palette[0];
                }
                return HabitDialog.Palette[_selectedIndex];
            }
        }

        public PaletteStrip()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
                | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
        }

        public void SelectColor(string hex)
        {
            for (int i = 0; i < HabitDialog.Palette.Length; i++)
            {
                if (string.Equals(HabitDialog.Palette[i], hex, StringComparison.OrdinalIgnoreCase))
                {
                    _selectedIndex = i;
                    this.Invalidate();
                    return;
                }
            }
        }

        private int CellSize
        {
            get
            {
                int n = HabitDialog.Palette.Length;
                int byWidth = n == 0 ? 28 : (Width - (n - 1) * 6) / n;
                return Math.Min(byWidth, Height - 12);
            }
        }

        private int IndexAt(Point p)
        {
            int cell = CellSize;
            if (cell <= 0)
            {
                return -1;
            }
            int step = cell + 6;
            int i = p.X / step;
            if (i < 0 || i >= HabitDialog.Palette.Length)
            {
                return -1;
            }
            return i;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor == Color.Transparent ? Color.White : BackColor);

            int cell = CellSize;
            if (cell <= 0)
            {
                return;
            }
            int top = (Height - cell) / 2;

            for (int i = 0; i < HabitDialog.Palette.Length; i++)
            {
                Rectangle r = new Rectangle(i * (cell + 6), top, cell, cell);
                Color c = Theme.Hex(HabitDialog.Palette[i]);
                Theme.FillRounded(g, r, 6, c);

                if (i == _selectedIndex)
                {
                    Theme.DrawRoundedBorder(g, Rectangle.Inflate(r, 3, 3), 8, Theme.TextMain, 2f);
                }
                else if (i == _hoverIndex)
                {
                    Theme.DrawRoundedBorder(g, Rectangle.Inflate(r, 3, 3), 8, Theme.TextFaint, 1.5f);
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int i = IndexAt(e.Location);
            if (i != _hoverIndex)
            {
                _hoverIndex = i;
                this.Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (_hoverIndex != -1)
            {
                _hoverIndex = -1;
                this.Invalidate();
            }
            base.OnMouseLeave(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            int i = IndexAt(e.Location);
            if (i >= 0 && i != _selectedIndex)
            {
                _selectedIndex = i;
                this.Invalidate();
                EventHandler handler = SelectionChanged;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            }
            base.OnMouseClick(e);
        }
    }
}

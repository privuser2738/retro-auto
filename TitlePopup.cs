using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RetroAuto
{
    public class TitlePopup : Form
    {
        private Label titleLabel = null!;
        private Label fileLabel = null!;
        private Label counterLabel = null!;
        private System.Windows.Forms.Timer closeTimer = null!;

        public TitlePopup(string romPath, int gameNumber, int totalGames)
        {
            InitializeComponents(romPath, gameNumber, totalGames);
        }

        private void InitializeComponents(string romPath, int gameNumber, int totalGames)
        {
            // Form settings
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.TopMost = true;
            this.Size = new Size(800, 300);
            this.BackColor = Color.FromArgb(20, 20, 30);
            this.Opacity = 0.95;

            // Add border
            this.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(100, 150, 255), 3))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, this.Width - 1, this.Height - 1);
                }
            };

            // Game title label
            titleLabel = new Label
            {
                Text = Path.GetFileNameWithoutExtension(romPath),
                Font = new Font("Segoe UI", 24, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = false,
                Size = new Size(760, 120),
                Location = new Point(20, 60),
                TextAlign = ContentAlignment.MiddleCenter
            };

            // File name label
            fileLabel = new Label
            {
                Text = Path.GetFileName(romPath),
                Font = new Font("Consolas", 12),
                ForeColor = Color.FromArgb(180, 180, 180),
                AutoSize = false,
                Size = new Size(760, 30),
                Location = new Point(20, 180),
                TextAlign = ContentAlignment.MiddleCenter
            };

            // Counter label
            counterLabel = new Label
            {
                Text = $"Game {gameNumber} of {totalGames}",
                Font = new Font("Segoe UI", 14),
                ForeColor = Color.FromArgb(100, 150, 255),
                AutoSize = false,
                Size = new Size(760, 30),
                Location = new Point(20, 20),
                TextAlign = ContentAlignment.MiddleCenter
            };

            this.Controls.Add(titleLabel);
            this.Controls.Add(fileLabel);
            this.Controls.Add(counterLabel);

            // Auto-close timer (3 seconds)
            closeTimer = new System.Windows.Forms.Timer();
            closeTimer.Interval = 3000;
            closeTimer.Tick += (s, e) =>
            {
                closeTimer.Stop();
                this.Close();
            };
        }

        public static async Task ShowBrieflyAsync(string romPath, int gameNumber, int totalGames)
        {
            var tcs = new TaskCompletionSource<bool>();

            var thread = new System.Threading.Thread(() =>
            {
                Application.EnableVisualStyles();
                var popup = new TitlePopup(romPath, gameNumber, totalGames);
                popup.Load += (s, e) =>
                {
                    popup.closeTimer.Start();
                };
                popup.FormClosed += (s, e) =>
                {
                    tcs.SetResult(true);
                };
                Application.Run(popup);
            });

            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();

            await tcs.Task;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Text.Json;
using AxWMPLib;

namespace ElectronicBell
{
    public class BellEvent
    {
        public string Name { get; set; } = "Звънец";
        public List<DayOfWeek> Days { get; set; } = new List<DayOfWeek>();
        public DateTime Time { get; set; }
        public string Source { get; set; } = "";
        public int DurationSeconds { get; set; } = 60;
        public bool IsPlayedToday { get; set; } = false;
    }

    public partial class MainForm : Form
    {
        private AxWindowsMediaPlayer mediaPlayer;
        private Timer scheduleTimer;
        private List<BellEvent> schedule = new List<BellEvent>();
        private string scheduleFile = "bell_schedule.json";

        private CheckBox chkWorkdays;
        private CheckBox chkSaturday;
        private CheckBox chkSunday;
        private DateTimePicker dtpTime;
        private TextBox txtSource;
        private NumericUpDown numDuration;
        private DataGridView dgvSchedule;
        private TextBox txtLog;

        public MainForm()
        {
            InitializeMyComponents();
            LoadSchedule();
            RefreshScheduleGrid();
            SetupScheduleTimer();
        }

        private void InitializeMyComponents()
        {
            this.Text = "Електронен звънец - Работни дни";
            this.Size = new Size(1030, 850);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormClosing += MainForm_FormClosing;

            // Media Player
            mediaPlayer = new AxWindowsMediaPlayer();
            ((System.ComponentModel.ISupportInitialize)(mediaPlayer)).BeginInit();
            mediaPlayer.Dock = DockStyle.Bottom;
            mediaPlayer.Height = 90;
            this.Controls.Add(mediaPlayer);
            ((System.ComponentModel.ISupportInitialize)(mediaPlayer)).EndInit();

            TabControl tabControl = new TabControl { Dock = DockStyle.Fill };
            this.Controls.Add(tabControl);

            TabPage tabSchedule = new TabPage("Разписание");
            tabControl.TabPages.Add(tabSchedule);

            // === Контроли ===
            Label lblType = new Label { Text = "Прилага се за:", Left = 20, Top = 25, AutoSize = true };

            chkWorkdays = new CheckBox { Text = "Работни дни (Понеделник - Петък)", Left = 20, Top = 50, Width = 320, Checked = true };
            chkSaturday = new CheckBox { Text = "Събота", Left = 20, Top = 78, Width = 150 };
            chkSunday = new CheckBox { Text = "Неделя", Left = 180, Top = 78, Width = 150 };

            dtpTime = new DateTimePicker { Left = 20, Top = 115, Width = 140, Format = DateTimePickerFormat.Time, ShowUpDown = true };

            txtSource = new TextBox { Left = 20, Top = 155, Width = 530, Text = @"http://192.168.10.46/listen/radio/radio.mp3" };

            numDuration = new NumericUpDown
            {
                Left = 560,
                Top = 155,
                Width = 100,
                Minimum = 10,
                Maximum = 3600,
                Increment = 10,
                Value = 60
            };

            // Бутони
            Button btnAdd = new Button { Text = "Добави в разписание", Left = 680, Top = 150, Width = 160, Height = 35 };
            Button btnTest = new Button { Text = "▶ Тестово пускане", Left = 680, Top = 190, Width = 160, Height = 35, BackColor = Color.LightGreen };
            Button btnDelete = new Button { Text = "Изтрий избраното", Left = 850, Top = 150, Width = 130, Height = 35 };
            Button btnSave = new Button { Text = "Запази ръчно", Left = 850, Top = 190, Width = 130, Height = 35 };

            // Panel за скролване
            Panel panel = new Panel
            {
                Left = 20,
                Top = 235,
                Width = 950,
                Height = 380,
                AutoScroll = true,
                BorderStyle = BorderStyle.FixedSingle
            };

            dgvSchedule = new DataGridView
            {
                Left = 0,
                Top = 0,
                Width = 930,
                Height = 720,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                ReadOnly = true,
                AllowUserToAddRows = false
            };
            panel.Controls.Add(dgvSchedule);

            txtLog = new TextBox
            {
                Left = 20,
                Top = 630,
                Width = 950,
                Height = 130,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                Font = new Font("Consolas", 9.5f),
                BackColor = Color.Black,
                ForeColor = Color.LightGreen
            };

            tabSchedule.Controls.AddRange(new Control[] { lblType, chkWorkdays, chkSaturday, chkSunday,
                dtpTime, txtSource, numDuration, btnAdd, btnTest, btnDelete, btnSave, panel, txtLog });

            // Събития
            btnAdd.Click += BtnAdd_Click;
            btnTest.Click += BtnTest_Click;
            btnDelete.Click += BtnDelete_Click;
            btnSave.Click += (s, e) => { SaveSchedule(); AddToLog("✅ Разписанието е запазено ръчно."); };

            AddToLog("✅ Програмата е стартирана успешно.");
        }

        private void BtnAdd_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtSource.Text))
            {
                MessageBox.Show("Моля въведете път към файл или URL!", "Внимание");
                return;
            }

            var ev = new BellEvent
            {
                Time = dtpTime.Value,
                Source = txtSource.Text.Trim(),
                DurationSeconds = (int)numDuration.Value
            };

            if (chkWorkdays.Checked)
                ev.Days.AddRange(new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday });

            if (chkSaturday.Checked) ev.Days.Add(DayOfWeek.Saturday);
            if (chkSunday.Checked) ev.Days.Add(DayOfWeek.Sunday);

            if (ev.Days.Count == 0)
            {
                MessageBox.Show("Моля изберете поне един ден!", "Внимание");
                return;
            }

            schedule.Add(ev);
            SaveSchedule();
            RefreshScheduleGrid();
            AddToLog($"✅ Добавено събитие за {ev.Days.Count} дни");
        }

        private void BtnTest_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtSource.Text))
            {
                MessageBox.Show("Моля въведете източник за тестване!", "Внимание");
                return;
            }

            var testEvent = new BellEvent
            {
                Source = txtSource.Text.Trim(),
                DurationSeconds = (int)numDuration.Value
            };

            PlayBell(testEvent);
            AddToLog("🧪 Стартирано тестово пускане");
        }

        private void CheckAndPlayScheduled()
        {
            var now = DateTime.Now;
            var today = now.DayOfWeek;

            var dueEvents = schedule.Where(ev =>
                ev.Days.Contains(today) &&
                Math.Abs((ev.Time.TimeOfDay - now.TimeOfDay).TotalSeconds) <= 20 &&
                !ev.IsPlayedToday).ToList();

            foreach (var ev in dueEvents)
            {
                PlayBell(ev);
                ev.IsPlayedToday = true;
            }
        }

        private void PlayBell(BellEvent ev)
        {
            try
            {
                if (string.IsNullOrEmpty(ev.Source)) return;

                mediaPlayer.URL = ev.Source;
                mediaPlayer.Ctlcontrols.play();

                int duration = Math.Max(ev.DurationSeconds, 10);

                var stopTimer = new Timer { Interval = duration * 1000 };
                stopTimer.Tick += (s, a) =>
                {
                    mediaPlayer?.Ctlcontrols.stop();
                    stopTimer.Dispose();
                };
                stopTimer.Start();

                AddToLog($"▶ ПУСНАТ: {ev.Source} | {duration} секунди");
            }
            catch (Exception ex)
            {
                AddToLog("❌ Грешка при пускане: " + ex.Message);
            }
        }

        private void SetupScheduleTimer()
        {
            scheduleTimer = new Timer { Interval = 10000 };
            scheduleTimer.Tick += (s, e) => CheckAndPlayScheduled();
            scheduleTimer.Start();
        }

        private void RefreshScheduleGrid()
        {
            dgvSchedule.DataSource = null;
            dgvSchedule.DataSource = schedule.Select(ev => new
            {
                Дни = string.Join(", ", ev.Days.Select(d => d.ToString().Substring(0, 3))),
                Час = ev.Time.ToString("HH:mm"),
                Източник = ev.Source.Length > 55 ? ev.Source.Substring(0, 52) + "..." : ev.Source,
                Продължителност = ev.DurationSeconds + " сек"
            }).ToList();
        }

        private void LoadSchedule()
        {
            if (File.Exists(scheduleFile))
            {
                try
                {
                    string json = File.ReadAllText(scheduleFile);
                    schedule = JsonSerializer.Deserialize<List<BellEvent>>(json) ?? new List<BellEvent>();
                    AddToLog($"📂 Заредени {schedule.Count} събития");
                }
                catch (Exception ex)
                {
                    AddToLog("⚠ Грешка при зареждане: " + ex.Message);
                }
            }
        }

        private void SaveSchedule()
        {
            try
            {
                string json = JsonSerializer.Serialize(schedule, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(scheduleFile, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Грешка при запазване:\n" + ex.Message);
            }
        }

        private void BtnDelete_Click(object sender, EventArgs e)
        {
            if (dgvSchedule.CurrentRow == null)
            {
                MessageBox.Show("Моля изберете събитие за изтриване.");
                return;
            }

            schedule.RemoveAt(dgvSchedule.CurrentRow.Index);
            SaveSchedule();
            RefreshScheduleGrid();
            AddToLog("🗑 Събитието е изтрито");
        }

        private void AddToLog(string text)
        {
            string line = $"[{DateTime.Now:HH:mm:ss}] {text}\n";
            if (txtLog?.InvokeRequired == true)
                txtLog.Invoke(new Action(() => { txtLog.AppendText(line); txtLog.ScrollToCaret(); }));
            else if (txtLog != null)
            {
                txtLog.AppendText(line);
                txtLog.ScrollToCaret();
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            SaveSchedule();
        }
    }
}
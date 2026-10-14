using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using ricxOptimizer.Core;
using ricxOptimizer.Models;

namespace ricxOptimizer.UI
{
    public class MainForm : Form
    {
        private readonly BackupEngine _backupEngine;
        private readonly OptimizationEngine _optEngine;

        // UI Controls
        private Label _lblTitle = null;
        private Label _lblSubtitle = null;
        private Label _lblAdminStatus = null;
        private Label _lblBackupStatus = null;
        private Label _lblSysInfo = null;
        private Button _btnCreateBackup = null;
        private Button _btnModeDefault = null;
        private Button _btnModeGaming = null;
        private Button _btnModeUltra = null;
        private Button _btnRestart = null;
        private Button _btnOpenBackupDir = null;
        private RichTextBox _rtbLogs = null;
        private ProgressBar _progressBar = null;

        // Visual Colors (Modern Gamer Dark Palette)
        private static readonly Color BgDark = Color.FromArgb(18, 18, 24);
        private static readonly Color BgCard = Color.FromArgb(28, 28, 38);
        private static readonly Color AccentCyan = Color.FromArgb(0, 229, 255);
        private static readonly Color AccentPurple = Color.FromArgb(168, 85, 247);
        private static readonly Color AccentRed = Color.FromArgb(244, 63, 94);
        private static readonly Color AccentGreen = Color.FromArgb(34, 197, 94);
        private static readonly Color TextWhite = Color.FromArgb(240, 240, 245);
        private static readonly Color TextMuted = Color.FromArgb(160, 160, 175);

        public MainForm()
        {
            _backupEngine = new BackupEngine();
            _optEngine = new OptimizationEngine(_backupEngine);

            InitializeComponent();
            CheckEnvironment();
        }

        private void InitializeComponent()
        {
            this.Text = "ricxOptimizer v1.0 - Suíte Profissional de Otimização de SO para Games";
            this.Size = new Size(1000, 780);
            this.MinimumSize = new Size(950, 720);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = BgDark;
            this.ForeColor = TextWhite;
            this.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

            // Main Layout Container
            var mainPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(20),
                BackColor = BgDark
            };
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 95));  // Header
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));  // Backup Bar & Admin status
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 220)); // 3 Action Cards
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Log Console
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));  // Footer Bar

            // 1. HEADER
            var headerPanel = new Panel { Dock = DockStyle.Fill };
            _lblTitle = new Label
            {
                Text = "⚡ ricxOptimizer",
                Font = new Font("Segoe UI", 20f, FontStyle.Bold),
                ForeColor = AccentCyan,
                Location = new Point(0, 5),
                AutoSize = true
            };
            _lblSubtitle = new Label
            {
                Text = "Desenvolvido por Especialista em Engenharia de SO e Otimização Extrema de Latência para Games",
                Font = new Font("Segoe UI", 10f, FontStyle.Italic),
                ForeColor = TextMuted,
                Location = new Point(5, 42),
                AutoSize = true
            };
            _lblSysInfo = new Label
            {
                Text = $"SO: {Environment.OSVersion.VersionString} | Máquina: {Environment.MachineName} | Cores Lógicos: {Environment.ProcessorCount}",
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                ForeColor = AccentPurple,
                Location = new Point(5, 68),
                AutoSize = true
            };
            headerPanel.Controls.AddRange(new Control[] { _lblTitle, _lblSubtitle, _lblSysInfo });

            // 2. STATUS & BACKUP ACTION BAR
            var statusBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Height = 45
            };

            _lblAdminStatus = new Label
            {
                AutoSize = true,
                Padding = new Padding(8, 6, 8, 6),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Margin = new Padding(0, 3, 15, 0)
            };

            _lblBackupStatus = new Label
            {
                AutoSize = true,
                Padding = new Padding(8, 6, 8, 6),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Margin = new Padding(0, 3, 20, 0)
            };

            _btnCreateBackup = CreateCustomButton("💾 Criar Backup de Segurança Agora", AccentCyan, Color.Black);
            _btnCreateBackup.Width = 300;
            _btnCreateBackup.Click += async (s, e) => await HandleCreateBackup();

            _btnOpenBackupDir = CreateCustomButton("📂 Abrir Pasta de Backups", BgCard, TextWhite);
            _btnOpenBackupDir.Width = 200;
            _btnOpenBackupDir.Click += (s, e) => OpenBackupFolder();

            statusBar.Controls.AddRange(new Control[] { _lblAdminStatus, _lblBackupStatus, _btnCreateBackup, _btnOpenBackupDir });

            // 3. THREE OPTIMIZATION CARDS (Default, Gaming, Ultra)
            var cardsPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Margin = new Padding(0, 5, 0, 5)
            };
            cardsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            cardsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            cardsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));

            // Card 1: Default
            var cardDefault = CreateCard(
                "🛡️ Windows Default",
                "Restaura o Windows para o funcionamento padrão original usando o backup de segurança. Reativa Defender, Windows Search, SysMain, spoolsv, MMCSS e energia Equilibrada.",
                "RESTAURAR WINDOWS PADRÃO",
                Color.FromArgb(59, 130, 246),
                out _btnModeDefault
            );
            _btnModeDefault.Click += async (s, e) => await ApplyModeWithConfirmation(OptimizationMode.WindowsDefault);

            // Card 2: Gaming Mode
            var cardGaming = CreateCard(
                "🎮 Windows Gaming Mode",
                "Otimização forte e equilibrada para jogos: Ultimate Performance, CPU Core Unparking, MMCSS priorizado, HAGS, GameDVR off, Nagle TCP off e DisablePagingExecutive.",
                "ATIVAR MODO GAMING",
                AccentPurple,
                out _btnModeGaming
            );
            _btnModeGaming.Click += async (s, e) => await ApplyModeWithConfirmation(OptimizationMode.WindowsGamingMode);

            // Card 3: Ultra Gaming
            var cardUltra = CreateCard(
                "⚡ Windows Gaming Ultra",
                "100% Gamer Extremo (Ideal para VM e Testes): Desativa Defender em tempo real, SmartScreen, VBS/HVCI (-25% latência), Spectre/Meltdown, SysMain, indexação, e timer 0.5ms.",
                "ATIVAR MODO ULTRA GAMER",
                AccentRed,
                out _btnModeUltra
            );
            _btnModeUltra.Click += async (s, e) => await ApplyModeWithConfirmation(OptimizationMode.WindowsGamingUltra);

            cardsPanel.Controls.Add(cardDefault, 0, 0);
            cardsPanel.Controls.Add(cardGaming, 1, 0);
            cardsPanel.Controls.Add(cardUltra, 2, 0);

            // 4. LOG TERMINAL
            var logGroup = new GroupBox
            {
                Text = " Console de Operações em Tempo Real ",
                Dock = DockStyle.Fill,
                ForeColor = AccentCyan,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
            };
            _rtbLogs = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(12, 12, 16),
                ForeColor = Color.FromArgb(160, 230, 160),
                Font = new Font("Consolas", 9f, FontStyle.Regular),
                BorderStyle = BorderStyle.None,
                ReadOnly = true
            };
            logGroup.Controls.Add(_rtbLogs);

            // 5. FOOTER (Progress bar and quick actions)
            var footerPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1
            };
            footerPanel.ColumnStyles.Add(new RowStyle(SizeType.Percent, 60));
            footerPanel.ColumnStyles.Add(new RowStyle(SizeType.Percent, 20));
            footerPanel.ColumnStyles.Add(new RowStyle(SizeType.Percent, 20));

            _progressBar = new ProgressBar
            {
                Dock = DockStyle.Fill,
                Height = 25,
                Style = ProgressBarStyle.Continuous,
                Margin = new Padding(0, 10, 10, 10)
            };

            var btnClearLogs = CreateCustomButton("Limpar Logs", BgCard, TextWhite);
            btnClearLogs.Click += (s, e) => _rtbLogs.Clear();

            _btnRestart = CreateCustomButton("🔄 Reiniciar PC", Color.FromArgb(180, 40, 40), TextWhite);
            _btnRestart.Click += (s, e) => ConfirmAndRestart();

            footerPanel.Controls.Add(_progressBar, 0, 0);
            footerPanel.Controls.Add(btnClearLogs, 1, 0);
            footerPanel.Controls.Add(_btnRestart, 2, 0);

            // Assemble Form
            mainPanel.Controls.Add(headerPanel, 0, 0);
            mainPanel.Controls.Add(statusBar, 0, 1);
            mainPanel.Controls.Add(cardsPanel, 0, 2);
            mainPanel.Controls.Add(logGroup, 0, 3);
            mainPanel.Controls.Add(footerPanel, 0, 4);

            this.Controls.Add(mainPanel);
        }

        private Panel CreateCard(string title, string description, string buttonText, Color accentColor, out Button actionButton)
        {
            var pnl = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = BgCard,
                Padding = new Padding(12),
                Margin = new Padding(6)
            };

            var lblT = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = accentColor,
                Dock = DockStyle.Top,
                Height = 32
            };

            var lblD = new Label
            {
                Text = description,
                Font = new Font("Segoe UI", 9f),
                ForeColor = TextMuted,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.TopLeft
            };

            actionButton = CreateCustomButton(buttonText, accentColor, Color.Black);
            actionButton.Dock = DockStyle.Bottom;
            actionButton.Height = 44;
            actionButton.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);

            pnl.Controls.Add(lblD);
            pnl.Controls.Add(lblT);
            pnl.Controls.Add(actionButton);

            return pnl;
        }

        private Button CreateCustomButton(string text, Color bg, Color fg)
        {
            var btn = new Button
            {
                Text = text,
                BackColor = bg,
                ForeColor = fg,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Height = 38
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        private void CheckEnvironment()
        {
            bool isAdmin = Win32Native.IsAdministrator();
            if (isAdmin)
            {
                _lblAdminStatus.Text = "✔ Privilégios de Administrador: ATIVO";
                _lblAdminStatus.BackColor = Color.FromArgb(20, 60, 30);
                _lblAdminStatus.ForeColor = AccentGreen;
            }
            else
            {
                _lblAdminStatus.Text = "⚠ AVISO: Execute como Administrador";
                _lblAdminStatus.BackColor = Color.FromArgb(80, 20, 20);
                _lblAdminStatus.ForeColor = AccentRed;
                AppendLog("AVISO", "O aplicativo não está em modo Administrador! Execute ricxOptimizer como Administrador para poder aplicar alterações no BCD, Registro e Serviços do Windows.", false);
            }

            UpdateBackupStatusUI();
            AppendLog("INIT", "ricxOptimizer pronto. Selecione o modo desejado ou crie um backup de segurança prévio.", true);
        }

        private void UpdateBackupStatusUI()
        {
            bool hasBackup = _backupEngine.HasExistingBackup();
            if (hasBackup)
            {
                _lblBackupStatus.Text = "✔ Backup de Segurança: ATIVO & DISPONÍVEL";
                _lblBackupStatus.BackColor = Color.FromArgb(20, 60, 30);
                _lblBackupStatus.ForeColor = AccentGreen;
            }
            else
            {
                _lblBackupStatus.Text = "⚠ Backup de Segurança: PENDENTE";
                _lblBackupStatus.BackColor = Color.FromArgb(80, 60, 20);
                _lblBackupStatus.ForeColor = Color.FromArgb(250, 204, 21);
            }
        }

        private async Task HandleCreateBackup()
        {
            SetButtonsEnabled(false);
            _progressBar.Style = ProgressBarStyle.Marquee;

            await Task.Run(() =>
            {
                _backupEngine.CreateFullBackup(log =>
                {
                    this.Invoke((MethodInvoker)delegate { AppendLog(log.Category, log.Message, log.Success); });
                });
            });

            _progressBar.Style = ProgressBarStyle.Continuous;
            _progressBar.Value = 100;
            UpdateBackupStatusUI();
            SetButtonsEnabled(true);

            MessageBox.Show(
                "Backup completo do Windows criado com sucesso!\n\nForam salvos:\n- Ponto de Restauração VSS\n- Configurações do BCD\n- Chaves Críticas do Registro\n- Estado dos Serviços\n- Plano de Energia Ativo",
                "ricxOptimizer - Backup Concluído",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }

        private async Task ApplyModeWithConfirmation(OptimizationMode mode)
        {
            string modeName = "Modo Desconhecido";
            switch (mode)
            {
                case OptimizationMode.WindowsDefault:
                    modeName = "Windows Default (Restauração)";
                    break;
                case OptimizationMode.WindowsGamingMode:
                    modeName = "Windows Gaming Mode (Equilibrado)";
                    break;
                case OptimizationMode.WindowsGamingUltra:
                    modeName = "Windows Gaming Ultra (100% Gamer Extremo)";
                    break;
            }

            string warning = "";
            switch (mode)
            {
                case OptimizationMode.WindowsDefault:
                    warning = "Isso irá retornar as configurações do Windows para o estado normal/backup.";
                    break;
                case OptimizationMode.WindowsGamingMode:
                    warning = "Isso ativará otimizações profundas de latência, plano Ultimate Performance e priorização de jogos.";
                    break;
                case OptimizationMode.WindowsGamingUltra:
                    warning = "ATENÇÃO: O Modo Ultra desativará o Windows Defender, VBS/HVCI, SysMain, mitigations e serviços em segundo plano para extrair 100% do desempenho gamer na máquina virtual. Deseja continuar?";
                    break;
            }

            var confirm = MessageBox.Show(
                $"{warning}\n\nConfirma a aplicação do {modeName}?",
                $"ricxOptimizer - Confirmar {modeName}",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (confirm != DialogResult.Yes) return;

            SetButtonsEnabled(false);
            _progressBar.Style = ProgressBarStyle.Marquee;

            await Task.Run(() =>
            {
                _optEngine.ApplyMode(mode, log =>
                {
                    this.Invoke((MethodInvoker)delegate { AppendLog(log.Category, log.Message, log.Success); });
                });
            });

            _progressBar.Style = ProgressBarStyle.Continuous;
            _progressBar.Value = 100;
            UpdateBackupStatusUI();
            SetButtonsEnabled(true);

            var restartAsk = MessageBox.Show(
                $"{modeName} foi aplicado com sucesso!\n\nPara que todas as diretivas do Kernel, BCD, Hypervisor e agendador entrem em vigor, é recomendável reiniciar o Windows.\n\nDeseja reiniciar agora?",
                "ricxOptimizer - Otimização Concluída",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information
            );

            if (restartAsk == DialogResult.Yes)
            {
                ConfirmAndRestart();
            }
        }

        private void SetButtonsEnabled(bool enabled)
        {
            _btnCreateBackup.Enabled = enabled;
            _btnModeDefault.Enabled = enabled;
            _btnModeGaming.Enabled = enabled;
            _btnModeUltra.Enabled = enabled;
        }

        private void AppendLog(string category, string message, bool success)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            Color logColor = success ? Color.FromArgb(180, 240, 180) : Color.FromArgb(250, 120, 120);

            _rtbLogs.SelectionStart = _rtbLogs.TextLength;
            _rtbLogs.SelectionLength = 0;

            _rtbLogs.SelectionColor = Color.FromArgb(120, 120, 140);
            _rtbLogs.AppendText($"[{timestamp}] ");

            _rtbLogs.SelectionColor = AccentCyan;
            _rtbLogs.AppendText($"[{category}] ");

            _rtbLogs.SelectionColor = logColor;
            _rtbLogs.AppendText($"{message}{Environment.NewLine}");

            _rtbLogs.ScrollToCaret();
        }

        private void OpenBackupFolder()
        {
            try
            {
                string dir = _backupEngine.GetBackupPath();
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                System.Diagnostics.Process.Start("explorer.exe", dir);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Não foi possível abrir a pasta de backups: {ex.Message}");
            }
        }

        private void ConfirmAndRestart()
        {
            var res = MessageBox.Show("Tem certeza que deseja reiniciar o sistema agora?", "Confirmar Reinicialização", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (res == DialogResult.Yes)
            {
                try
                {
                    System.Diagnostics.Process.Start("shutdown.exe", "/r /t 5 /c \"ricxOptimizer reiniciando o sistema para aplicar novas diretivas de desempenho\"");
                }
                catch { }
            }
        }
    }
}

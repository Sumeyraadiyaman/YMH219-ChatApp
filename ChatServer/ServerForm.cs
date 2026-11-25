using System.Net;
using System.Net.Sockets;
using ChatShared;

namespace ChatServer
{
    // Sunucu penceresi: sunucuyu başlatır/durdurur, bağlı kullanıcıları ve olay kaydını gösterir.
    // Arayüz kodla oluşturulmuştur (BuildUi metodu).
    public class ServerForm : Form
    {
        private readonly ChatServerCore server = new ChatServerCore();

        private NumericUpDown numPort = null!;
        private Button btnStart = null!;
        private Button btnStop = null!;
        private Button btnSaveLog = null!;
        private Label lblAddress = null!;
        private Label lblClients = null!;
        private ListBox lstClients = null!;
        private RichTextBox txtLog = null!;
        private Label lblStatus = null!;

        public ServerForm()
        {
            BuildUi();

            // Sunucu çekirdeğinin olaylarını dinle
            server.LogMessage += Server_LogMessage;
            server.ClientsChanged += Server_ClientsChanged;

            btnStart.Click += BtnStart_Click;
            btnStop.Click += BtnStop_Click;
            btnSaveLog.Click += BtnSaveLog_Click;
            FormClosing += ServerForm_FormClosing;

            lblAddress.Text = "Bu bilgisayarın IP adresi: " + GetLocalIpAddresses();
            UpdateControls();
        }

        #region Olaylar

        private void BtnStart_Click(object? sender, EventArgs e)
        {
            try
            {
                server.Start((int)numPort.Value);
            }
            catch (SocketException ex)
            {
                MessageBox.Show($"Sunucu başlatılamadı. {numPort.Value} portu başka bir program tarafından kullanılıyor olabilir.\n\nAyrıntı: {ex.Message}",
                    "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            UpdateControls();
        }

        private void BtnStop_Click(object? sender, EventArgs e)
        {
            server.Stop();
            UpdateControls();
        }

        // Sunucu çekirdeğinden gelen kayıt satırı (arka plan iş parçacığından gelir)
        private void Server_LogMessage(string text)
        {
            RunOnUi(() =>
            {
                txtLog.AppendText(text + Environment.NewLine);
                txtLog.ScrollToCaret();
            });
        }

        // Bağlı kullanıcı listesi değişti (arka plan iş parçacığından gelir)
        private void Server_ClientsChanged(List<ConnectedClient> clients)
        {
            RunOnUi(() =>
            {
                lstClients.Items.Clear();
                foreach (ConnectedClient client in clients)
                    lstClients.Items.Add(client); // ListBox, ToString() sonucunu gösterir

                lblClients.Text = $"Bağlı kullanıcılar ({clients.Count})";
                UpdateControls();
            });
        }

        // Olay kaydını .txt dosyasına kaydeder
        private void BtnSaveLog_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtLog.Text))
            {
                MessageBox.Show("Kaydedilecek kayıt yok.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using SaveFileDialog dialog = new SaveFileDialog
            {
                Filter = "Metin dosyası (*.txt)|*.txt",
                FileName = $"sunucu_kaydi_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            };

            if (dialog.ShowDialog() != DialogResult.OK)
                return;

            try
            {
                File.WriteAllText(dialog.FileName, txtLog.Text);
                MessageBox.Show("Kayıt dosyaya yazıldı:\n" + dialog.FileName, "Başarılı",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Dosya kaydedilemedi:\n" + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ServerForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            server.LogMessage -= Server_LogMessage;
            server.ClientsChanged -= Server_ClientsChanged;
            server.Stop();
        }

        #endregion

        #region Yardımcı metotlar

        // Butonları ve durum yazısını sunucunun durumuna göre ayarlar
        private void UpdateControls()
        {
            btnStart.Enabled = !server.IsRunning;
            btnStop.Enabled = server.IsRunning;
            numPort.Enabled = !server.IsRunning;

            if (server.IsRunning)
            {
                lblStatus.Text = $"Durum: Çalışıyor (port {server.Port}) — {lstClients.Items.Count} kullanıcı bağlı";
                lblStatus.ForeColor = ToColor(Theme.Mint);
            }
            else
            {
                lblStatus.Text = "Durum: Durduruldu";
                lblStatus.ForeColor = ToColor(Theme.Red);
            }
        }

        // Diğer bilgisayarların bağlanabilmesi için bu bilgisayarın yerel IP adresleri
        private static string GetLocalIpAddresses()
        {
            try
            {
                IEnumerable<string> addresses = Dns.GetHostAddresses(Dns.GetHostName())
                    .Where(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a))
                    .Select(a => a.ToString());

                string text = string.Join(", ", addresses);
                return text.Length > 0 ? text + "  (aynı bilgisayar için: 127.0.0.1)" : "127.0.0.1";
            }
            catch (SocketException)
            {
                return "127.0.0.1";
            }
        }

        // Arayüzü değiştiren kodu arayüz iş parçacığında çalıştırır
        private void RunOnUi(Action action)
        {
            if (IsDisposed || !IsHandleCreated)
                return;

            if (InvokeRequired)
                BeginInvoke(action);
            else
                action();
        }

        private static Color ToColor((int R, int G, int B) c) => Color.FromArgb(c.R, c.G, c.B);

        #endregion

        #region Arayüz

        // Pencere ve kontrolleri oluşturur
        private void BuildUi()
        {
            Text = "Chat Sunucusu";
            ClientSize = new Size(720, 520);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = ToColor(Theme.Background);
            Font = new Font("Segoe UI", 9.5F);

            Label lblPort = new Label { Text = "Port:", Location = new Point(15, 20), AutoSize = true };
            numPort = new NumericUpDown { Location = new Point(60, 16), Width = 90, Minimum = 1024, Maximum = 65535, Value = 5000 };

            btnStart = CreateButton("▶ Sunucuyu Başlat", Theme.Mint, new Point(170, 12), 170);
            btnStop = CreateButton("■ Durdur", Theme.Red, new Point(350, 12), 120);
            btnSaveLog = CreateButton("Kaydı Kaydet (.txt)", Theme.LightMint, new Point(545, 12), 160);

            lblAddress = new Label { Location = new Point(15, 58), AutoSize = true, ForeColor = Color.DimGray };

            lblClients = new Label
            {
                Text = "Bağlı kullanıcılar (0)",
                Location = new Point(15, 88),
                AutoSize = true,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = ToColor(Theme.Pink)
            };
            lstClients = new ListBox
            {
                Location = new Point(15, 112),
                Size = new Size(260, 360),
                BorderStyle = BorderStyle.FixedSingle,
                ForeColor = ToColor(Theme.Text)
            };

            Label lblLog = new Label
            {
                Text = "Olay kaydı",
                Location = new Point(290, 88),
                AutoSize = true,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = ToColor(Theme.Mint)
            };
            txtLog = new RichTextBox
            {
                Location = new Point(290, 112),
                Size = new Size(415, 360),
                ReadOnly = true,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                ForeColor = ToColor(Theme.Text),
                Font = new Font("Consolas", 9.5F)
            };

            lblStatus = new Label
            {
                Location = new Point(15, 485),
                AutoSize = true,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold)
            };

            Controls.AddRange(new Control[]
            {
                lblPort, numPort, btnStart, btnStop, btnSaveLog, lblAddress,
                lblClients, lstClients, lblLog, txtLog, lblStatus
            });
        }

        // Düz, renkli bir buton oluşturur; pasifken otomatik griye döner
        private static Button CreateButton(string text, (int R, int G, int B) color, Point location, int width)
        {
            Color active = ToColor(color);
            Color disabled = ToColor(Theme.Disabled);

            Button button = new Button
            {
                Text = text,
                Location = location,
                Size = new Size(width, 34),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = active,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            button.FlatAppearance.BorderSize = 0;
            button.EnabledChanged += (s, e) => button.BackColor = button.Enabled ? active : disabled;

            return button;
        }

        #endregion
    }
}

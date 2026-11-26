using System.Net.Sockets;
using ChatShared;

namespace ChatClient
{
    // İstemci penceresi: sunucuya bağlanma, mesajlaşma, çevrimiçi kullanıcılar ve özel mesaj.
    // Arayüz kodla oluşturulmuştur (BuildUi metodu).
    public class ClientForm : Form
    {
        private const string EveryoneOption = "Herkes";

        private readonly ChatClientCore client = new ChatClientCore();

        private TextBox txtHost = null!;
        private NumericUpDown numPort = null!;
        private TextBox txtUserName = null!;
        private Button btnConnect = null!;
        private Button btnDisconnect = null!;
        private RichTextBox txtChat = null!;
        private Label lblUsers = null!;
        private ListBox lstUsers = null!;
        private Button btnSaveHistory = null!;
        private ComboBox cmbRecipient = null!;
        private TextBox txtMessage = null!;
        private Button btnSend = null!;
        private Label lblStatus = null!;

        public ClientForm()
        {
            BuildUi();

            // Ağ tarafının olaylarını dinle
            client.MessageReceived += Client_MessageReceived;
            client.UserListReceived += Client_UserListReceived;
            client.Disconnected += Client_Disconnected;

            btnConnect.Click += BtnConnect_Click;
            btnDisconnect.Click += BtnDisconnect_Click;
            btnSend.Click += BtnSend_Click;
            btnSaveHistory.Click += BtnSaveHistory_Click;
            txtMessage.KeyDown += TxtMessage_KeyDown;
            lstUsers.DoubleClick += LstUsers_DoubleClick;
            FormClosing += ClientForm_FormClosing;

            UpdateControls();
        }

        #region Bağlantı

        private async void BtnConnect_Click(object? sender, EventArgs e)
        {
            string host = txtHost.Text.Trim();
            string userName = txtUserName.Text.Trim();

            if (host.Length == 0 || userName.Length == 0)
            {
                MessageBox.Show("Lütfen sunucu adresini ve kullanıcı adını girin.", "Eksik bilgi",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (userName.Length > 20)
            {
                MessageBox.Show("Kullanıcı adı en fazla 20 karakter olabilir.", "Geçersiz ad",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnConnect.Enabled = false;
            SetStatus("Bağlanılıyor...", Color.DimGray);

            try
            {
                await client.ConnectAsync(host, (int)numPort.Value, userName);
                txtChat.Clear();
                AppendLine($"[{DateTime.Now:HH:mm:ss}] * {host}:{numPort.Value} adresine \"{userName}\" olarak bağlandınız.", Color.Gray, italic: true);
                txtMessage.Focus();
            }
            catch (OperationCanceledException)
            {
                ShowError("Sunucu 5 saniye içinde yanıt vermedi (zaman aşımı). IP adresini ve sunucunun açık olduğunu kontrol edin.");
            }
            catch (SocketException)
            {
                ShowError("Sunucuya bağlanılamadı. Sunucunun çalıştığından, IP adresinin ve portun doğru olduğundan emin olun.");
            }
            catch (Exception ex)
            {
                ShowError("Bağlantı kurulamadı: " + ex.Message);
            }

            UpdateControls();
        }

        private async void BtnDisconnect_Click(object? sender, EventArgs e)
        {
            await client.DisconnectAsync();
            AppendLine($"[{DateTime.Now:HH:mm:ss}] * Bağlantıyı kestiniz.", Color.Gray, italic: true);
            ClearUsers();
            UpdateControls();
        }

        // Bağlantı kullanıcı istemeden koptu (sunucu kapandı, ad alınmış, ağ gitti...)
        private void Client_Disconnected(string reason)
        {
            RunOnUi(() =>
            {
                AppendLine($"[{DateTime.Now:HH:mm:ss}] ! {reason}", ToColor(Theme.Red), italic: false);
                ClearUsers();
                UpdateControls();
                MessageBox.Show(reason, "Bağlantı kesildi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            });
        }

        #endregion

        #region Mesajlaşma

        private async void BtnSend_Click(object? sender, EventArgs e)
        {
            await SendCurrentMessageAsync();
        }

        // Mesaj kutusunda Enter'a basınca gönder
        private async void TxtMessage_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true; // "Bip" sesini ve satır atlamayı engelle
                await SendCurrentMessageAsync();
            }
        }

        private async Task SendCurrentMessageAsync()
        {
            string text = txtMessage.Text.Trim();
            if (text.Length == 0 || !client.IsConnected)
                return;

            string? recipient = cmbRecipient.SelectedItem as string;
            if (recipient == EveryoneOption)
                recipient = null;

            try
            {
                await client.SendAsync(text, recipient);
                txtMessage.Clear();
            }
            catch (Exception ex)
            {
                ShowError("Mesaj gönderilemedi: " + ex.Message);
            }

            txtMessage.Focus();
        }

        // Sunucudan mesaj geldi (arka plan iş parçacığından gelir)
        private void Client_MessageReceived(ChatMessage message)
        {
            RunOnUi(() =>
            {
                Color color;
                bool italic = false;

                if (message.Type == MessageType.System)
                {
                    color = Color.Gray;
                    italic = true;
                }
                else if (message.Type == MessageType.Private)
                {
                    color = ToColor(Theme.Purple);
                }
                else if (message.Sender == client.UserName)
                {
                    color = ToColor(Theme.Pink);   // Kendi mesajlarım pembe
                }
                else
                {
                    color = ToColor(Theme.Text);
                }

                AppendLine(message.ToDisplayString(), color, italic);
            });
        }

        // Çevrimiçi kullanıcı listesi güncellendi (arka plan iş parçacığından gelir)
        private void Client_UserListReceived(List<string> users)
        {
            RunOnUi(() =>
            {
                string? previousRecipient = cmbRecipient.SelectedItem as string;

                lstUsers.Items.Clear();
                cmbRecipient.Items.Clear();
                cmbRecipient.Items.Add(EveryoneOption);

                foreach (string user in users)
                {
                    lstUsers.Items.Add(user == client.UserName ? user + " (sen)" : user);
                    if (user != client.UserName)
                        cmbRecipient.Items.Add(user);
                }

                // Önceden seçili kişi hâlâ bağlıysa seçimi koru
                int index = previousRecipient == null ? -1 : cmbRecipient.Items.IndexOf(previousRecipient);
                cmbRecipient.SelectedIndex = index >= 0 ? index : 0;

                lblUsers.Text = $"Çevrimiçi ({users.Count})";
            });
        }

        // Listede bir kişiye çift tıklayınca ona özel mesaj yazmaya geç
        private void LstUsers_DoubleClick(object? sender, EventArgs e)
        {
            if (lstUsers.SelectedItem is not string selected || selected.EndsWith(" (sen)"))
                return;

            cmbRecipient.SelectedItem = selected;
            txtMessage.Focus();
        }

        // Sohbet geçmişini .txt dosyasına kaydeder
        private void BtnSaveHistory_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtChat.Text))
            {
                MessageBox.Show("Kaydedilecek mesaj yok.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using SaveFileDialog dialog = new SaveFileDialog
            {
                Filter = "Metin dosyası (*.txt)|*.txt",
                FileName = $"sohbet_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            };

            if (dialog.ShowDialog() != DialogResult.OK)
                return;

            try
            {
                File.WriteAllText(dialog.FileName, txtChat.Text);
                MessageBox.Show("Sohbet geçmişi kaydedildi:\n" + dialog.FileName, "Başarılı",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                ShowError("Dosya kaydedilemedi: " + ex.Message);
            }
        }

        private void ClientForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            client.MessageReceived -= Client_MessageReceived;
            client.UserListReceived -= Client_UserListReceived;
            client.Disconnected -= Client_Disconnected;
            _ = client.DisconnectAsync(); // Sunucuya ayrıldığımızı bildir
        }

        #endregion

        #region Yardımcı metotlar

        // Sohbet kutusuna renkli bir satır ekler
        private void AppendLine(string text, Color color, bool italic)
        {
            txtChat.SelectionStart = txtChat.TextLength;
            txtChat.SelectionLength = 0;
            txtChat.SelectionColor = color;
            txtChat.SelectionFont = new Font(txtChat.Font, italic ? FontStyle.Italic : FontStyle.Regular);
            txtChat.AppendText(text + Environment.NewLine);
            txtChat.ScrollToCaret();
        }

        private void ClearUsers()
        {
            lstUsers.Items.Clear();
            cmbRecipient.Items.Clear();
            cmbRecipient.Items.Add(EveryoneOption);
            cmbRecipient.SelectedIndex = 0;
            lblUsers.Text = "Çevrimiçi (0)";
        }

        // Butonları ve durum yazısını bağlantı durumuna göre ayarlar
        private void UpdateControls()
        {
            bool connected = client.IsConnected;

            btnConnect.Enabled = !connected;
            btnDisconnect.Enabled = connected;
            btnSend.Enabled = connected;
            txtMessage.Enabled = connected;
            cmbRecipient.Enabled = connected;
            txtHost.Enabled = !connected;
            numPort.Enabled = !connected;
            txtUserName.Enabled = !connected;

            if (connected)
                SetStatus($"Bağlı: {client.UserName}", ToColor(Theme.Mint));
            else
                SetStatus("Bağlı değil", ToColor(Theme.Red));
        }

        private void SetStatus(string text, Color color)
        {
            lblStatus.Text = "Durum: " + text;
            lblStatus.ForeColor = color;
        }

        private void ShowError(string message)
        {
            MessageBox.Show(message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            Text = "Chat İstemcisi";
            ClientSize = new Size(820, 560);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = ToColor(Theme.Background);
            Font = new Font("Segoe UI", 9.5F);

            // Üst satır: bağlantı bilgileri
            Label lblHost = new Label { Text = "Sunucu IP:", Location = new Point(15, 20), AutoSize = true };
            txtHost = new TextBox { Text = "127.0.0.1", Location = new Point(90, 16), Width = 120 };
            Label lblPort = new Label { Text = "Port:", Location = new Point(220, 20), AutoSize = true };
            numPort = new NumericUpDown { Location = new Point(260, 16), Width = 75, Minimum = 1024, Maximum = 65535, Value = 5000 };
            Label lblName = new Label { Text = "Kullanıcı adı:", Location = new Point(345, 20), AutoSize = true };
            txtUserName = new TextBox { Location = new Point(435, 16), Width = 100, MaxLength = 20 };

            btnConnect = CreateButton("Bağlan", Theme.Mint, new Point(545, 12), 100);
            btnDisconnect = CreateButton("Bağlantıyı Kes", Theme.Red, new Point(653, 12), 152);

            // Orta: sohbet alanı ve kullanıcı listesi
            txtChat = new RichTextBox
            {
                Location = new Point(15, 60),
                Size = new Size(570, 400),
                ReadOnly = true,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10.5F)
            };

            lblUsers = new Label
            {
                Text = "Çevrimiçi (0)",
                Location = new Point(600, 60),
                AutoSize = true,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = ToColor(Theme.Pink)
            };
            lstUsers = new ListBox
            {
                Location = new Point(600, 85),
                Size = new Size(205, 325),
                BorderStyle = BorderStyle.FixedSingle,
                ForeColor = ToColor(Theme.Text)
            };
            btnSaveHistory = CreateButton("Geçmişi Kaydet (.txt)", Theme.LightMint, new Point(600, 422), 205);

            // Alt satır: mesaj yazma
            Label lblTo = new Label { Text = "Kime:", Location = new Point(15, 479), AutoSize = true };
            cmbRecipient = new ComboBox
            {
                Location = new Point(60, 475),
                Width = 130,
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat
            };
            txtMessage = new TextBox { Location = new Point(200, 475), Width = 480, MaxLength = 500 };
            btnSend = CreateButton("Gönder ➤", Theme.Pink, new Point(690, 470), 115);

            lblStatus = new Label
            {
                Location = new Point(15, 525),
                AutoSize = true,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold)
            };

            Controls.AddRange(new Control[]
            {
                lblHost, txtHost, lblPort, numPort, lblName, txtUserName, btnConnect, btnDisconnect,
                txtChat, lblUsers, lstUsers, btnSaveHistory,
                lblTo, cmbRecipient, txtMessage, btnSend, lblStatus
            });

            ClearUsers();
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

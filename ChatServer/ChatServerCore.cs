using System.Net;
using System.Net.Sockets;
using ChatShared;

namespace ChatServer
{
    // Sunucunun asıl işini yapan sınıf: bağlantıları kabul eder,
    // gelen mesajları herkese (veya özel mesajsa tek kişiye) dağıtır.
    // Arayüzden bağımsızdır; arayüze olaylar (event) ile haber verir.
    public class ChatServerCore
    {
        private TcpListener? listener;
        private CancellationTokenSource? cancellation;

        private readonly List<ConnectedClient> clients = new List<ConnectedClient>();
        private readonly object clientsLock = new object(); // Listeye aynı anda erişimi korur

        public bool IsRunning { get; private set; }
        public int Port { get; private set; }

        // Arayüze haber vermek için olaylar (arka plan iş parçacığından tetiklenir!)
        public event Action<string>? LogMessage;
        public event Action<List<ConnectedClient>>? ClientsChanged;

        // Sunucuyu belirtilen portta başlatır (port kullanılıyorsa SocketException fırlatır)
        public void Start(int port)
        {
            if (IsRunning)
                return;

            listener = new TcpListener(IPAddress.Any, port); // Tüm ağ kartlarından dinle
            listener.Start();

            Port = port;
            cancellation = new CancellationTokenSource();
            IsRunning = true;

            Log($"Sunucu {port} portunda başlatıldı. Bağlantılar bekleniyor...");
            _ = AcceptLoopAsync(cancellation.Token); // Arka planda bağlantı beklemeye başla
        }

        // Sunucuyu durdurur ve tüm bağlantıları kapatır
        public void Stop()
        {
            if (!IsRunning)
                return;

            IsRunning = false;
            cancellation?.Cancel();
            listener?.Stop();

            List<ConnectedClient> snapshot;
            lock (clientsLock)
            {
                snapshot = clients.ToList();
                clients.Clear();
            }

            foreach (ConnectedClient client in snapshot)
                client.Connection.Dispose();

            Log("Sunucu durduruldu.");
            RaiseClientsChanged();
        }

        // Yeni bağlantıları sürekli bekler
        private async Task AcceptLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                TcpClient tcpClient;
                try
                {
                    tcpClient = await listener!.AcceptTcpClientAsync(token);
                }
                catch (OperationCanceledException)
                {
                    break; // Sunucu durduruldu
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (SocketException) when (token.IsCancellationRequested)
                {
                    break;
                }
                catch (SocketException ex)
                {
                    Log("Bağlantı kabul edilemedi: " + ex.Message);
                    continue;
                }

                // Her istemciyi ayrı bir görevde dinle ki biri diğerini bekletmesin
                _ = HandleClientAsync(tcpClient, token);
            }
        }

        // Tek bir istemcinin bağlantısını baştan sona yönetir
        private async Task HandleClientAsync(TcpClient tcpClient, CancellationToken token)
        {
            ChatConnection connection = new ChatConnection(tcpClient);
            ConnectedClient? client = null;

            try
            {
                // 1) İlk mesaj mutlaka kullanıcı adını bildiren Join olmalı
                ChatMessage? first = await connection.ReceiveAsync(token);
                if (first == null || first.Type != MessageType.Join || string.IsNullOrWhiteSpace(first.Sender))
                    return;

                string userName = first.Sender.Trim();

                // 2) Aynı isimde biri varsa reddet
                if (IsUserNameTaken(userName))
                {
                    await connection.SendAsync(new ChatMessage
                    {
                        Type = MessageType.Error,
                        Text = $"\"{userName}\" kullanıcı adı zaten kullanılıyor. Başka bir ad deneyin."
                    });
                    Log($"Reddedildi: \"{userName}\" adı zaten kullanılıyor ({connection.RemoteAddress})");
                    return;
                }

                // 3) Listeye ekle ve herkese duyur
                client = new ConnectedClient(userName, connection);
                lock (clientsLock)
                {
                    clients.Add(client);
                }

                Log($"{userName} bağlandı ({connection.RemoteAddress})");
                RaiseClientsChanged();
                await BroadcastAsync(ChatMessage.CreateSystem($"{userName} sohbete katıldı."));
                await BroadcastUserListAsync();

                // 4) Bağlantı kapanana kadar mesajları dinle
                while (!token.IsCancellationRequested)
                {
                    ChatMessage? message = await connection.ReceiveAsync(token);
                    if (message == null || message.Type == MessageType.Leave)
                        break;

                    // Gönderen adını ve saati sunucu belirler; kimse başkası adına yazamaz
                    message.Sender = userName;
                    message.Time = DateTime.Now;

                    if (message.Type == MessageType.Chat)
                    {
                        Log($"{userName}: {message.Text}");
                        await BroadcastAsync(message);
                    }
                    else if (message.Type == MessageType.Private)
                    {
                        await SendPrivateAsync(client, message);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Sunucu durduruldu
            }
            catch (IOException)
            {
                // İstemcinin bağlantısı aniden koptu (pencereyi kapattı, ağ gitti vb.)
            }
            catch (ObjectDisposedException)
            {
                // Bağlantı zaten kapatılmış
            }
            catch (Exception ex)
            {
                Log("İstemci hatası: " + ex.Message);
            }
            finally
            {
                connection.Dispose();
                await RemoveClientAsync(client, token);
            }
        }

        // Ayrılan istemciyi listeden çıkarır ve diğerlerine haber verir
        private async Task RemoveClientAsync(ConnectedClient? client, CancellationToken token)
        {
            if (client == null)
                return;

            bool removed;
            lock (clientsLock)
            {
                removed = clients.Remove(client);
            }

            if (!removed || token.IsCancellationRequested)
                return; // Sunucu kapanıyorsa duyuru yapmaya gerek yok

            Log($"{client.UserName} ayrıldı.");
            RaiseClientsChanged();
            await BroadcastAsync(ChatMessage.CreateSystem($"{client.UserName} sohbetten ayrıldı."));
            await BroadcastUserListAsync();
        }

        // Özel mesajı sadece alıcıya ve gönderene iletir
        private async Task SendPrivateAsync(ConnectedClient sender, ChatMessage message)
        {
            ConnectedClient? recipient;
            lock (clientsLock)
            {
                recipient = clients.FirstOrDefault(c => c.UserName == message.Recipient);
            }

            if (recipient == null)
            {
                await TrySendAsync(sender, ChatMessage.CreateSystem($"\"{message.Recipient}\" artık bağlı değil."));
                return;
            }

            Log($"(özel) {sender.UserName} → {recipient.UserName}");
            await TrySendAsync(recipient, message);

            if (recipient != sender)
                await TrySendAsync(sender, message); // Gönderen de kendi mesajını görsün
        }

        // Mesajı bağlı herkese gönderir
        private async Task BroadcastAsync(ChatMessage message)
        {
            foreach (ConnectedClient client in GetClientsSnapshot())
                await TrySendAsync(client, message);
        }

        // Güncel kullanıcı listesini herkese gönderir
        private async Task BroadcastUserListAsync()
        {
            List<ConnectedClient> snapshot = GetClientsSnapshot();
            ChatMessage list = new ChatMessage
            {
                Type = MessageType.UserList,
                Users = snapshot.Select(c => c.UserName).ToList()
            };

            foreach (ConnectedClient client in snapshot)
                await TrySendAsync(client, list);
        }

        // Tek bir istemciye gönderir; o istemcinin bağlantısı kopmuşsa sessizce geçer
        private static async Task TrySendAsync(ConnectedClient client, ChatMessage message)
        {
            try
            {
                await client.Connection.SendAsync(message);
            }
            catch (Exception ex) when (ex is IOException || ex is ObjectDisposedException || ex is InvalidOperationException)
            {
                // Bu istemci zaten ayrılıyor; kendi döngüsü onu listeden çıkaracak
            }
        }

        private bool IsUserNameTaken(string userName)
        {
            lock (clientsLock)
            {
                return clients.Any(c => string.Equals(c.UserName, userName, StringComparison.OrdinalIgnoreCase));
            }
        }

        // Listenin o anki kopyası (dolaşırken liste değişirse hata olmasın diye)
        private List<ConnectedClient> GetClientsSnapshot()
        {
            lock (clientsLock)
            {
                return clients.ToList();
            }
        }

        private void RaiseClientsChanged()
        {
            ClientsChanged?.Invoke(GetClientsSnapshot());
        }

        private void Log(string text)
        {
            LogMessage?.Invoke($"[{DateTime.Now:HH:mm:ss}] {text}");
        }
    }
}

using System.Net.Sockets;
using ChatShared;

namespace ChatClient
{
    // İstemcinin ağ tarafı: sunucuya bağlanır, mesaj gönderir ve gelen mesajları dinler.
    // Arayüzden bağımsızdır; arayüze olaylar (event) ile haber verir.
    public class ChatClientCore
    {
        private ChatConnection? connection;
        private CancellationTokenSource? cancellation;

        public string UserName { get; private set; } = "";
        public bool IsConnected => connection != null;

        // Arayüze haber vermek için olaylar (arka plan iş parçacığından tetiklenir!)
        public event Action<ChatMessage>? MessageReceived;
        public event Action<List<string>>? UserListReceived;
        public event Action<string>? Disconnected; // Parametre: bağlantının neden koptuğu

        // Sunucuya bağlanır. Sunucuya ulaşılamazsa hata fırlatır (arayüz bunu yakalar).
        public async Task ConnectAsync(string host, int port, string userName)
        {
            if (IsConnected)
                return;

            TcpClient tcpClient = new TcpClient();
            try
            {
                // 5 saniye içinde bağlanamazsa vazgeç
                using CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await tcpClient.ConnectAsync(host, port, timeout.Token);
            }
            catch
            {
                tcpClient.Dispose();
                throw;
            }

            ChatConnection newConnection = new ChatConnection(tcpClient);
            UserName = userName;
            cancellation = new CancellationTokenSource();
            connection = newConnection;

            // Sunucuya kim olduğumuzu bildir
            try
            {
                await newConnection.SendAsync(new ChatMessage { Type = MessageType.Join, Sender = userName });
            }
            catch
            {
                Cleanup();
                throw;
            }

            // Gelen mesajları arka planda dinlemeye başla
            _ = ReceiveLoopAsync(newConnection, cancellation.Token);
        }

        // Herkese mesaj gönderir; recipient doluysa sadece o kişiye özel mesaj gönderir
        public async Task SendAsync(string text, string? recipient = null)
        {
            ChatConnection current = connection ?? throw new InvalidOperationException("Sunucuya bağlı değilsiniz.");

            ChatMessage message = new ChatMessage
            {
                Type = string.IsNullOrEmpty(recipient) ? MessageType.Chat : MessageType.Private,
                Sender = UserName,
                Recipient = recipient ?? "",
                Text = text
            };

            await current.SendAsync(message);
        }

        // Kullanıcı kendi isteğiyle bağlantıyı keser
        public async Task DisconnectAsync()
        {
            ChatConnection? current = connection;
            if (current == null)
                return;

            try
            {
                await current.SendAsync(new ChatMessage { Type = MessageType.Leave });
            }
            catch
            {
                // Bağlantı zaten kopmuşsa önemli değil
            }

            Cleanup();
        }

        // Sunucudan gelen mesajları bağlantı kapanana kadar dinler
        private async Task ReceiveLoopAsync(ChatConnection current, CancellationToken token)
        {
            string reason = "Sunucu bağlantıyı kapattı.";

            try
            {
                while (true)
                {
                    ChatMessage? message = await current.ReceiveAsync(token);
                    if (message == null)
                        break; // Sunucu kapandı

                    if (message.Type == MessageType.UserList)
                    {
                        UserListReceived?.Invoke(message.Users);
                    }
                    else if (message.Type == MessageType.Error)
                    {
                        reason = message.Text; // Ör. kullanıcı adı alınmış
                        break;
                    }
                    else
                    {
                        MessageReceived?.Invoke(message);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return; // Kullanıcı "Bağlantıyı Kes"e bastı
            }
            catch (Exception ex) when (ex is IOException || ex is ObjectDisposedException || ex is SocketException)
            {
                reason = "Sunucuyla bağlantı koptu: " + ex.Message;
            }

            if (token.IsCancellationRequested)
                return; // Kullanıcı kendisi kesti, ayrıca haber vermeye gerek yok

            Cleanup();
            Disconnected?.Invoke(reason);
        }

        // Bağlantıyı kapatır (iki farklı yerden aynı anda çağrılsa da güvenli)
        private void Cleanup()
        {
            ChatConnection? current = Interlocked.Exchange(ref connection, null);
            cancellation?.Cancel();
            current?.Dispose();
        }
    }
}

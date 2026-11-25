using System.Net.Sockets;
using System.Text;

namespace ChatShared
{
    // Bir TCP bağlantısı üzerinden ChatMessage gönderip almayı sağlayan sınıf.
    // Her mesaj tek satır JSON olarak yazılır; satır sonu mesajın bittiğini gösterir.
    public class ChatConnection : IDisposable
    {
        private readonly TcpClient client;
        private readonly StreamReader reader;
        private readonly StreamWriter writer;

        // Aynı anda iki yerden yazılırsa mesajlar birbirine karışmasın diye kilit
        private readonly SemaphoreSlim writeLock = new SemaphoreSlim(1, 1);

        public ChatConnection(TcpClient client)
        {
            this.client = client;
            NetworkStream stream = client.GetStream();

            UTF8Encoding utf8 = new UTF8Encoding(false); // Türkçe karakterler için UTF-8
            reader = new StreamReader(stream, utf8);
            writer = new StreamWriter(stream, utf8) { AutoFlush = true };
        }

        // Karşı tarafın IP adresi ve portu (ör. 192.168.1.5:54321)
        public string RemoteAddress => client.Client.RemoteEndPoint?.ToString() ?? "bilinmiyor";

        // Mesajı gönderir (bağlantı kopmuşsa IOException fırlatır)
        public async Task SendAsync(ChatMessage message)
        {
            await writeLock.WaitAsync();
            try
            {
                await writer.WriteLineAsync(message.ToJson());
            }
            finally
            {
                writeLock.Release();
            }
        }

        // Sıradaki mesajı bekler. Karşı taraf bağlantıyı kapattıysa null döner.
        public async Task<ChatMessage?> ReceiveAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                string? line = await reader.ReadLineAsync(cancellationToken);
                if (line == null)
                    return null; // Bağlantı kapandı

                ChatMessage? message = ChatMessage.FromJson(line);
                if (message != null)
                    return message;

                // Bozuk satır geldiyse yok say ve sıradakini bekle
            }
        }

        // Bağlantıyı kapatır
        public void Dispose()
        {
            try
            {
                client.Close();
            }
            catch
            {
                // Zaten kapalıysa sorun değil
            }
        }
    }
}

using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChatShared
{
    // Sunucu ile istemci arasında gidip gelen mesaj türleri
    public enum MessageType
    {
        Join,       // İstemci kullanıcı adıyla katılmak istiyor
        Leave,      // İstemci ayrılıyor
        Chat,       // Herkese giden normal mesaj
        Private,    // Tek bir kişiye giden özel mesaj
        System,     // Sunucunun duyurusu ("Ayşe katıldı" gibi)
        UserList,   // Bağlı kullanıcıların listesi
        Error       // Hata (ör. kullanıcı adı alınmış) — bağlantı kapanır
    }

    // Ağ üzerinden gönderilen tek bir mesaj.
    // Sunucu ve istemci bu sınıfı ortak kullandığı için birbirlerini her zaman anlar.
    public class ChatMessage
    {
        public MessageType Type { get; set; }
        public string Sender { get; set; } = "";
        public string Recipient { get; set; } = "";   // Sadece özel mesajlarda dolu
        public string Text { get; set; } = "";
        public DateTime Time { get; set; } = DateTime.Now;
        public List<string> Users { get; set; } = new List<string>(); // Sadece UserList'te dolu

        // Enum'lar JSON'da sayı yerine okunabilir yazı olarak görünsün ("Chat" gibi)
        private static readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions
        {
            Converters = { new JsonStringEnumConverter() }
        };

        // Mesajı tek satırlık JSON yazısına çevirir (ağdan bu şekilde gönderilir)
        public string ToJson()
        {
            return JsonSerializer.Serialize(this, jsonOptions);
        }

        // JSON yazısını tekrar mesaj nesnesine çevirir; bozuksa null döner
        public static ChatMessage? FromJson(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<ChatMessage>(json, jsonOptions);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        // Kısa yoldan sistem mesajı oluşturur
        public static ChatMessage CreateSystem(string text)
        {
            return new ChatMessage { Type = MessageType.System, Sender = "Sunucu", Text = text };
        }

        // Ekranda gösterilecek hali: [14:03:22] Sümeyra: merhaba
        public string ToDisplayString()
        {
            string time = $"[{Time:HH:mm:ss}]";

            return Type switch
            {
                MessageType.Chat => $"{time} {Sender}: {Text}",
                MessageType.Private => $"{time} (özel) {Sender} → {Recipient}: {Text}",
                MessageType.System => $"{time} * {Text}",
                MessageType.Error => $"{time} ! {Text}",
                _ => $"{time} {Text}"
            };
        }
    }
}

using ChatShared;

namespace ChatServer
{
    // Sunucuya bağlı tek bir kullanıcı
    public class ConnectedClient
    {
        public string UserName { get; }
        public ChatConnection Connection { get; }
        public DateTime ConnectedAt { get; }

        public ConnectedClient(string userName, ChatConnection connection)
        {
            UserName = userName;
            Connection = connection;
            ConnectedAt = DateTime.Now;
        }

        // Sunucu ekranındaki listede böyle görünür: Sümeyra  (127.0.0.1:54321)  bağlandı 14:03
        public override string ToString()
        {
            return $"{UserName}  ({Connection.RemoteAddress})  bağlandı {ConnectedAt:HH:mm}";
        }
    }
}

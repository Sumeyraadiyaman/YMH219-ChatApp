namespace ChatServer
{
    internal static class Program
    {
        // Uygulamanın başlangıç noktası: sunucu penceresini açar
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new ServerForm());
        }
    }
}

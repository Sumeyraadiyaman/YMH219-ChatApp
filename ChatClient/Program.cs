namespace ChatClient
{
    internal static class Program
    {
        // Uygulamanın başlangıç noktası: istemci penceresini açar
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new ClientForm());
        }
    }
}

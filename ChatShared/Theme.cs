namespace ChatShared
{
    // Sunucu ve istemcinin ortak renk paleti (pembe - mint)
    // Not: Bu kütüphane Windows Forms kullanmadığı için renkler RGB sayıları olarak tutulur.
    public static class Theme
    {
        public static readonly (int R, int G, int B) Background = (253, 246, 249); // Çok açık pembe
        public static readonly (int R, int G, int B) Pink = (236, 64, 122);
        public static readonly (int R, int G, int B) Mint = (38, 166, 154);
        public static readonly (int R, int G, int B) LightMint = (77, 182, 172);
        public static readonly (int R, int G, int B) Red = (239, 83, 80);
        public static readonly (int R, int G, int B) Gray = (176, 176, 176);
        public static readonly (int R, int G, int B) Disabled = (215, 215, 215);
        public static readonly (int R, int G, int B) Text = (55, 45, 50);
        public static readonly (int R, int G, int B) Purple = (156, 39, 176);
    }
}

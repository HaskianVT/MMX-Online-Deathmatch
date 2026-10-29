namespace MMXOnline
{
    public static class Md5
    {
        public static byte[] hash(byte[] data) => System.Security.Cryptography.MD5.HashData(data);
    }
}

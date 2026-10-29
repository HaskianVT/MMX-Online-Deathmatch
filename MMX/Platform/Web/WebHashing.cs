using System;
using System.Buffers.Binary;

namespace MMXOnline
{
    // .NET doesn't support MD5 in the browser, but the game's checksums must match the desktop's (RFC 1321)
    public static class Md5
    {
        static readonly int[] shifts =
        {
            7, 12, 17, 22, 7, 12, 17, 22, 7, 12, 17, 22, 7, 12, 17, 22,
            5, 9, 14, 20, 5, 9, 14, 20, 5, 9, 14, 20, 5, 9, 14, 20,
            4, 11, 16, 23, 4, 11, 16, 23, 4, 11, 16, 23, 4, 11, 16, 23,
            6, 10, 15, 21, 6, 10, 15, 21, 6, 10, 15, 21, 6, 10, 15, 21
        };

        static readonly uint[] constants = createConstants();

        static uint[] createConstants()
        {
            var k = new uint[64];
            for (int i = 0; i < 64; i++) k[i] = (uint)(long)Math.Floor(Math.Abs(Math.Sin(i + 1)) * 4294967296.0);
            return k;
        }

        public static byte[] hash(byte[] data)
        {
            // Pad to a multiple of 64 bytes: 0x80, zeros, then the length in bits
            int paddedLength = ((data.Length + 8) / 64 + 1) * 64;
            var message = new byte[paddedLength];
            Buffer.BlockCopy(data, 0, message, 0, data.Length);
            message[data.Length] = 0x80;
            BinaryPrimitives.WriteUInt64LittleEndian(message.AsSpan(paddedLength - 8), (ulong)data.Length * 8);

            uint a0 = 0x67452301, b0 = 0xefcdab89, c0 = 0x98badcfe, d0 = 0x10325476;
            var m = new uint[16];
            for (int chunk = 0; chunk < paddedLength; chunk += 64)
            {
                for (int i = 0; i < 16; i++) m[i] = BinaryPrimitives.ReadUInt32LittleEndian(message.AsSpan(chunk + i * 4));

                uint a = a0, b = b0, c = c0, d = d0;
                for (int i = 0; i < 64; i++)
                {
                    uint f;
                    int g;
                    if (i < 16) { f = (b & c) | (~b & d); g = i; }
                    else if (i < 32) { f = (d & b) | (~d & c); g = (5 * i + 1) % 16; }
                    else if (i < 48) { f = b ^ c ^ d; g = (3 * i + 5) % 16; }
                    else { f = c ^ (b | ~d); g = (7 * i) % 16; }

                    f = f + a + constants[i] + m[g];
                    a = d;
                    d = c;
                    c = b;
                    b += (f << shifts[i]) | (f >> (32 - shifts[i]));
                }
                a0 += a; b0 += b; c0 += c; d0 += d;
            }

            var result = new byte[16];
            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(0), a0);
            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(4), b0);
            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(8), c0);
            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(12), d0);
            return result;
        }
    }
}

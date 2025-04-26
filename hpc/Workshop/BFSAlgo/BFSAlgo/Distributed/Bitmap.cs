
using System.Runtime.CompilerServices;

namespace BFSAlgo.Distributed
{   
    public sealed class Bitmap
    {
        private readonly ulong[] bits;
        public int Size { get; }

        public Bitmap(int size)
        {
            Size = size;
            bits = new ulong[(size + 63) / 64]; // 64 bits per ulong
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(uint index)
        {
            // Below is the "easy" way to bitmap, but as 8 == 2^3 we can use bitwise operations - way faster!
            //bits[index / 8] |= (byte)(1 << (index % 8));

            int arrayIndex = (int)(index >> 6);       // divide by 64
            int bitPosition = (int)(index & 63);       // modulo 64
            bits[arrayIndex] |= 1UL << bitPosition;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Get(uint index)
        {
            int arrayIndex = (int)(index >> 6);
            int bitPosition = (int)(index & 63);
            return (bits[arrayIndex] & (1UL << bitPosition)) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Or(Bitmap other)
        {
            if (other.bits.Length != bits.Length)
                throw new InvalidOperationException("Bitmap sizes must match");

            for (int i = 0; i < bits.Length; i++)
            {
                bits[i] |= other.bits[i];
            }
        }

        public byte[] ToByteArray()
        {
            var bytes = new byte[bits.Length * 8];
            Buffer.BlockCopy(bits, 0, bytes, 0, bytes.Length);
            return bytes;
        }

        public static Bitmap FromByteArray(byte[] bytes, int size)
        {
            var bmp = new Bitmap(size);
            Buffer.BlockCopy(bytes, 0, bmp.bits, 0, bytes.Length);
            return bmp;
        }
    }

}

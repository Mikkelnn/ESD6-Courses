using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace BFSAlgo.Distributed
{   
    public sealed class Bitmap
    {
        private readonly ulong[] bits;
        public int ByteSize { get; }
        public int MaxNodeCount { get; }

        public ReadOnlyMemory<byte> AsReadOnlyMemory => new ReadOnlyMemory<byte>(MemoryMarshal.AsBytes(bits.AsSpan()).ToArray());

        public Bitmap(int maxNodeCount)
        {
            this.MaxNodeCount = maxNodeCount;            
            bits = new ulong[(maxNodeCount + 63) / 64]; // 64 bits per ulong
            ByteSize = bits.Length * sizeof(ulong);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(uint index)
        {
            if (index >= MaxNodeCount)
                throw new ArgumentOutOfRangeException(nameof(index), $"Index {index} is out of bounds for bitmap of size {MaxNodeCount}.");

            // Below is the "easy" way to bitmap, but as 64 == 2^6 we can use bitwise operations - way faster!
            //bits[index / 64] |= (1UL << (index % 64));

            int arrayIndex = (int)(index >> 6);       // divide by 64
            int bitPosition = (int)(index & 63);       // modulo 64
            bits[arrayIndex] |= 1UL << bitPosition;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Get(uint index)
        {
            if (index >= MaxNodeCount)
                throw new ArgumentOutOfRangeException(nameof(index), $"Index {index} is out of bounds for bitmap of size {MaxNodeCount}.");

            int arrayIndex = (int)(index >> 6);
            int bitPosition = (int)(index & 63);
            return (bits[arrayIndex] & (1UL << bitPosition)) != 0;
        }

        /// <summary>
        /// Set a index if it is not set alredy.
        /// </summary>
        /// <param name="index"></param>
        /// <returns><see langword="false"/> if the index is already set, and <see langword="true"/> if it was set</returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool SetIfNot(uint index)
        {
            if (index >= MaxNodeCount)
                throw new ArgumentOutOfRangeException(nameof(index), $"Index {index} is out of bounds for bitmap of size {MaxNodeCount}.");

            int arrayIndex = (int)(index >> 6);       // divide by 64
            int bitPosition = (int)(index & 63);       // modulo 64
            ulong mask = 1UL << bitPosition;

            if ((bits[arrayIndex] & mask) != 0) return false;
            
            bits[arrayIndex] |= mask;

            return true;
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

        public void OverwriteFromByteArray(byte[] bytes)
        {
            if (bytes.Length != ByteSize) throw new ArgumentException($"Input size {bytes}B does not match current size {ByteSize}B!");
            Buffer.BlockCopy(bytes, 0, bits, 0, bytes.Length);
        }

        public bool IsAllSet()
        {
            int lastNotFilledBits = 64 - (MaxNodeCount % 64); // nodeCount does not fill entire ulong

            for (int i = 0, lastIdx = bits.Length - 1; i <= lastIdx; i++)
            {
                // last is special case
                if (i == lastIdx && lastNotFilledBits != 64)
                {
                    ulong mask = ulong.MaxValue >> lastNotFilledBits;
                    if (bits[i] != mask) return false;
                }
                else if (bits[i] != ulong.MaxValue) return false;
            }

            return true;
        }
    }

}

namespace BFSAlgo.Distributed.Network
{
    public static class NetworkHelperExtensions
    {
        public static ReadOnlyMemory<byte> ToReadOnlyMemory(this List<uint> list)
        {
            var uintArray = list.ToArray();  // allocate once
            int bytes = uintArray.Length * sizeof(uint);
            var byteFrontier = new byte[bytes];
            Buffer.BlockCopy(uintArray, 0, byteFrontier, 0, bytes);
            return byteFrontier;
        }
    }
}

using System;
using System.Buffers;

namespace ModelContextProtocol.Core.Performance
{
    /// <summary>
    /// Buffer pool for reducing byte array allocations
    /// </summary>
    public static class BufferPool
    {
        private static readonly ArrayPool<byte> _arrayPool = ArrayPool<byte>.Shared;

        /// <summary>
        /// Rents a buffer from the pool
        /// </summary>
        /// <param name="minimumLength">Minimum buffer length</param>
        /// <returns>Rented buffer</returns>
        public static byte[] Rent(int minimumLength)
        {
            return _arrayPool.Rent(minimumLength);
        }

        /// <summary>
        /// Returns a buffer to the pool
        /// </summary>
        /// <param name="array">Buffer to return</param>
        /// <param name="clearArray">Whether to clear the buffer before returning</param>
        public static void Return(byte[] array, bool clearArray = false)
        {
            _arrayPool.Return(array, clearArray);
        }

        /// <summary>
        /// Rents a memory owner from the pool
        /// </summary>
        /// <param name="minimumLength">Minimum buffer length</param>
        /// <returns>Memory owner</returns>
        public static IMemoryOwner<byte> RentMemory(int minimumLength)
        {
            return new PooledMemory(_arrayPool, minimumLength);
        }

        private class PooledMemory : IMemoryOwner<byte>
        {
            private readonly ArrayPool<byte> _pool;
            private byte[] _array;
            private bool _disposed;

            public PooledMemory(ArrayPool<byte> pool, int minimumLength)
            {
                _pool = pool;
                _array = pool.Rent(minimumLength);
            }

            public Memory<byte> Memory => _array.AsMemory();

            public void Dispose()
            {
                if (!_disposed)
                {
                    _pool.Return(_array);
                    _array = null;
                    _disposed = true;
                }
            }
        }
    }
}

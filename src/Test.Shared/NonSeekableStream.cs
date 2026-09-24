namespace Test.Shared
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// A read-only, non-seekable stream over bytes, optionally returning at most a few bytes per read and optionally
    /// cancelling a token after a number of reads. Used to test buffering, slow sources and mid-read cancellation.
    /// </summary>
    public sealed class NonSeekableStream : Stream
    {
        private readonly byte[] _Data;
        private readonly int _MaxBytesPerRead;
        private readonly CancellationTokenSource? _CancelAfter;
        private readonly int _CancelAfterReads;
        private int _Position = 0;
        private int _Reads = 0;
        private bool _Disposed = false;

        /// <summary>
        /// Number of reads served.
        /// </summary>
        public int Reads
        {
            get => _Reads;
        }

        /// <summary>
        /// True once Dispose has been called.
        /// </summary>
        public bool IsDisposed
        {
            get => _Disposed;
        }

        /// <inheritdoc />
        public override bool CanRead
        {
            get => !_Disposed;
        }

        /// <inheritdoc />
        public override bool CanSeek
        {
            get => false;
        }

        /// <inheritdoc />
        public override bool CanWrite
        {
            get => false;
        }

        /// <inheritdoc />
        public override long Length
        {
            get { throw new NotSupportedException(); }
        }

        /// <inheritdoc />
        public override long Position
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

        /// <summary>
        /// Instantiate the stream.
        /// </summary>
        /// <param name="data">Content.</param>
        /// <param name="maxBytesPerRead">Largest number of bytes returned per read. Default unlimited.</param>
        /// <param name="cancelAfter">Token source to cancel once cancelAfterReads reads have been served. Optional.</param>
        /// <param name="cancelAfterReads">Reads before cancelling.</param>
        public NonSeekableStream(byte[] data, int maxBytesPerRead = int.MaxValue, CancellationTokenSource? cancelAfter = null, int cancelAfterReads = 0)
        {
            _Data = data;
            _MaxBytesPerRead = maxBytesPerRead < 1 ? 1 : maxBytesPerRead;
            _CancelAfter = cancelAfter;
            _CancelAfterReads = cancelAfterReads;
        }

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_Disposed) throw new ObjectDisposedException(nameof(NonSeekableStream));
            _Reads++;
            if (_CancelAfter != null && _Reads >= _CancelAfterReads) _CancelAfter.Cancel();
            int n = Math.Min(Math.Min(count, _MaxBytesPerRead), _Data.Length - _Position);
            if (n <= 0) return 0;
            Buffer.BlockCopy(_Data, _Position, buffer, offset, n);
            _Position += n;
            return n;
        }

        /// <inheritdoc />
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Read(buffer, offset, count));
        }

        /// <inheritdoc />
        public override void Flush()
        {
        }

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            _Disposed = true;
            base.Dispose(disposing);
        }
    }
}

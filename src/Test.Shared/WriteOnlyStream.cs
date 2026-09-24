namespace Test.Shared
{
    using System;
    using System.IO;

    /// <summary>
    /// A write-only, non-seekable stream that records what was written.
    /// </summary>
    public sealed class WriteOnlyStream : Stream
    {
        private readonly MemoryStream _Buffer = new MemoryStream();

        /// <summary>
        /// Everything written so far.
        /// </summary>
        public byte[] Written
        {
            get => _Buffer.ToArray();
        }

        /// <inheritdoc />
        public override bool CanRead
        {
            get => false;
        }

        /// <inheritdoc />
        public override bool CanSeek
        {
            get => false;
        }

        /// <inheritdoc />
        public override bool CanWrite
        {
            get => true;
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

        /// <inheritdoc />
        public override void Flush()
        {
        }

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
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
            _Buffer.Write(buffer, offset, count);
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing) _Buffer.Dispose();
            base.Dispose(disposing);
        }
    }
}

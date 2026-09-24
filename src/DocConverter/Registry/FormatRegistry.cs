namespace DocConverter.Registry
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using DocConverter.Enums;
    using DocConverter.Readers;
    using DocConverter.Readers.Delimited;
    using DocConverter.Readers.Docx;
    using DocConverter.Readers.Html;
    using DocConverter.Readers.Image;
    using DocConverter.Readers.Json;
    using DocConverter.Readers.Markdown;
    using DocConverter.Readers.Pdf;
    using DocConverter.Readers.Pptx;
    using DocConverter.Readers.Rtf;
    using DocConverter.Readers.Text;
    using DocConverter.Readers.Xlsx;
    using DocConverter.Readers.Xml;
    using DocConverter.Writers;
    using DocConverter.Writers.Delimited;
    using DocConverter.Writers.Docx;
    using DocConverter.Writers.Html;
    using DocConverter.Writers.Json;
    using DocConverter.Writers.Markdown;
    using DocConverter.Writers.Pdf;
    using DocConverter.Writers.Pptx;
    using DocConverter.Writers.Text;
    using DocConverter.Writers.Xlsx;
    using DocConverter.Writers.Xml;

    /// <summary>
    /// Maps each format to one reader and one writer. Read on every conversion and written rarely, so it is guarded by a
    /// ReaderWriterLockSlim. Thread safe.
    /// </summary>
    internal sealed class FormatRegistry : IDisposable
    {
        private readonly Dictionary<DocumentFormatEnum, IDocumentReader> _Readers = new Dictionary<DocumentFormatEnum, IDocumentReader>();
        private readonly Dictionary<DocumentFormatEnum, IDocumentWriter> _Writers = new Dictionary<DocumentFormatEnum, IDocumentWriter>();
        private readonly ReaderWriterLockSlim _Lock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        private bool _Disposed = false;

        internal static FormatRegistry CreateDefault()
        {
            FormatRegistry registry = new FormatRegistry();
            registry.RegisterReader(new TextDocumentReader());
            registry.RegisterReader(new MarkdownDocumentReader());
            registry.RegisterReader(new HtmlDocumentReader());
            registry.RegisterReader(new JsonDocumentReader());
            registry.RegisterReader(new XmlDocumentReader());
            registry.RegisterReader(new DelimitedDocumentReader());
            registry.RegisterReader(new RtfDocumentReader());
            registry.RegisterReader(new DocxDocumentReader());
            registry.RegisterReader(new XlsxDocumentReader());
            registry.RegisterReader(new PptxDocumentReader());
            registry.RegisterReader(new PdfDocumentReader());
            registry.RegisterReader(new ImageDocumentReader());

            registry.RegisterWriter(new MarkdownDocumentWriter());
            registry.RegisterWriter(new HtmlDocumentWriter());
            registry.RegisterWriter(new PlainTextDocumentWriter());
            registry.RegisterWriter(new JsonDocumentWriter());
            registry.RegisterWriter(new XmlDocumentWriter());
            registry.RegisterWriter(new DelimitedDocumentWriter());
            registry.RegisterWriter(new DocxDocumentWriter());
            registry.RegisterWriter(new XlsxDocumentWriter());
            registry.RegisterWriter(new PptxDocumentWriter());
            registry.RegisterWriter(new PdfDocumentWriter());
            return registry;
        }

        internal void RegisterReader(IDocumentReader reader)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));
            if (reader.Formats == null || reader.Formats.Count == 0) throw new ArgumentException("The reader declares no formats.", nameof(reader));
            foreach (DocumentFormatEnum format in reader.Formats)
                if (format == DocumentFormatEnum.Auto) throw new ArgumentException("A reader cannot register for Auto.", nameof(reader));

            _Lock.EnterWriteLock();
            try
            {
                foreach (DocumentFormatEnum format in reader.Formats) _Readers[format] = reader;
            }
            finally
            {
                _Lock.ExitWriteLock();
            }
        }

        internal void RegisterWriter(IDocumentWriter writer)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            if (writer.Formats == null || writer.Formats.Count == 0) throw new ArgumentException("The writer declares no formats.", nameof(writer));
            foreach (DocumentFormatEnum format in writer.Formats)
                if (format == DocumentFormatEnum.Auto) throw new ArgumentException("A writer cannot register for Auto.", nameof(writer));

            _Lock.EnterWriteLock();
            try
            {
                foreach (DocumentFormatEnum format in writer.Formats) _Writers[format] = writer;
            }
            finally
            {
                _Lock.ExitWriteLock();
            }
        }

        internal IDocumentReader? GetReader(DocumentFormatEnum format)
        {
            _Lock.EnterReadLock();
            try
            {
                return _Readers.TryGetValue(format, out IDocumentReader? reader) ? reader : null;
            }
            finally
            {
                _Lock.ExitReadLock();
            }
        }

        internal IDocumentWriter? GetWriter(DocumentFormatEnum format)
        {
            _Lock.EnterReadLock();
            try
            {
                return _Writers.TryGetValue(format, out IDocumentWriter? writer) ? writer : null;
            }
            finally
            {
                _Lock.ExitReadLock();
            }
        }

        internal List<DocumentFormatEnum> GetReaderFormats()
        {
            _Lock.EnterReadLock();
            try
            {
                List<DocumentFormatEnum> formats = new List<DocumentFormatEnum>(_Readers.Keys);
                formats.Sort();
                return formats;
            }
            finally
            {
                _Lock.ExitReadLock();
            }
        }

        internal List<DocumentFormatEnum> GetWriterFormats()
        {
            _Lock.EnterReadLock();
            try
            {
                List<DocumentFormatEnum> formats = new List<DocumentFormatEnum>(_Writers.Keys);
                formats.Sort();
                return formats;
            }
            finally
            {
                _Lock.ExitReadLock();
            }
        }

        public void Dispose()
        {
            if (_Disposed) return;
            _Disposed = true;
            _Lock.Dispose();
        }
    }
}

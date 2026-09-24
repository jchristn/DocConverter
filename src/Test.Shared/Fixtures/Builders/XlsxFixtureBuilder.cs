namespace Test.Shared.Fixtures.Builders
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using DocumentFormat.OpenXml;
    using DocumentFormat.OpenXml.Packaging;
    using DocumentFormat.OpenXml.Spreadsheet;
    using A = DocumentFormat.OpenXml.Drawing;
    using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;

    /// <summary>
    /// Builds XLSX fixtures with the raw OpenXml SDK, never with DocConverter's writer, so reader tests are independent.
    /// </summary>
    public static class XlsxFixtureBuilder
    {
        /// <summary>Serial date written in the Notes sheet (2023-02-15).</summary>
        public const double DateSerial = 44972;

        /// <summary>ISO form of DateSerial.</summary>
        public const string DateIso = "2023-02-15";

        /// <summary>Cached value of the formula cell in the Notes sheet.</summary>
        public const string FormulaCached = "59";

        /// <summary>Text in the hidden sheet.</summary>
        public const string HiddenText = "Secret hidden value";

        /// <summary>
        /// The reference workbook: sheet "Staff" holds ReferenceContent.TableRows (Years numeric), sheet "Notes" holds
        /// international and special text, a date, a formula with a cached value and a boolean, sheet "Hidden" is hidden,
        /// and the core properties carry the reference title and author.
        /// </summary>
        /// <returns>XLSX bytes.</returns>
        public static byte[] BuildReference()
        {
            return Build(delegate (XlsxWorkbookBuilder b)
            {
                List<object?[]> staff = new List<object?[]>();
                foreach (string[] row in ReferenceContent.TableRows)
                {
                    int years;
                    object? last = int.TryParse(row[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out years) ? (object)years : row[2];
                    staff.Add(new object?[] { row[0], row[1], last });
                }

                b.AddSheet("Staff", staff, false, null);
                b.AddSheet("Notes", new List<object?[]>
                {
                    new object?[] { "Label", "Value" },
                    new object?[] { "International", ReferenceContent.International },
                    new object?[] { "Special", ReferenceContent.Special },
                    new object?[] { "Hired", new XlsxDateCell(DateSerial) },
                    new object?[] { "Total", new XlsxFormulaCell("SUM(Staff!C2:C4)", FormulaCached) },
                    new object?[] { "Active", true }
                }, false, null);
                b.AddSheet("Hidden", new List<object?[]> { new object?[] { HiddenText, "x" }, new object?[] { "y", "z" } }, true, null);
            });
        }

        /// <summary>
        /// A sheet with merged cells: A1:B1 horizontal merge ("Merged header"), then rows, and a vertical merge A3:A4.
        /// </summary>
        /// <returns>XLSX bytes.</returns>
        public static byte[] BuildMerged()
        {
            return Build(delegate (XlsxWorkbookBuilder b)
            {
                b.AddSheet("Merged", new List<object?[]>
                {
                    new object?[] { "Merged header", null, "Other" },
                    new object?[] { "Name", "Role", "Team" },
                    new object?[] { "Tall cell", "Engineer", "Alpha" },
                    new object?[] { null, "Designer", "Beta" }
                }, false, new string[] { "A1:B1", "A3:A4" });
            });
        }

        /// <summary>
        /// A sheet with a single-cell title row above the table.
        /// </summary>
        /// <returns>XLSX bytes.</returns>
        public static byte[] BuildWithTitleRow()
        {
            return Build(delegate (XlsxWorkbookBuilder b)
            {
                b.AddSheet("Report", new List<object?[]>
                {
                    new object?[] { "Quarterly Report", null, null },
                    new object?[] { "Region", "Q1", "Q2" },
                    new object?[] { "North", 100, 120 },
                    new object?[] { "South", 90, 95 }
                }, false, null);
            });
        }

        /// <summary>
        /// A sheet whose drawing holds the reference PNG with alternative text.
        /// </summary>
        /// <returns>XLSX bytes.</returns>
        public static byte[] BuildWithImage()
        {
            return Build(delegate (XlsxWorkbookBuilder b)
            {
                b.AddSheet("Pictures", new List<object?[]> { new object?[] { "Caption", "Value" }, new object?[] { "Logo", 1 } }, false, null, ReferenceContent.ImagePng());
            });
        }

        /// <summary>
        /// A workbook with no rows at all.
        /// </summary>
        /// <returns>XLSX bytes.</returns>
        public static byte[] BuildEmpty()
        {
            return Build(delegate (XlsxWorkbookBuilder b)
            {
                b.AddSheet("Empty", new List<object?[]>(), false, null);
            });
        }

        /// <summary>
        /// A valid zip that lacks the workbook part.
        /// </summary>
        /// <returns>Zip bytes.</returns>
        public static byte[] BuildMissingWorkbook()
        {
            using (MemoryStream ms = new MemoryStream())
            {
                using (System.IO.Compression.ZipArchive zip = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Create, true))
                {
                    WriteEntry(zip, "[Content_Types].xml", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/></Types>");
                    WriteEntry(zip, "_rels/.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"></Relationships>");
                }

                return ms.ToArray();
            }
        }

        /// <summary>
        /// An OLE2 compound file header followed by the UTF-16LE stream name "EncryptedPackage", which is how password
        /// protected Office Open XML files are stored.
        /// </summary>
        /// <returns>Bytes.</returns>
        public static byte[] BuildEncrypted()
        {
            List<byte> bytes = new List<byte> { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 };
            bytes.AddRange(new byte[504]);
            bytes.AddRange(Encoding.Unicode.GetBytes("EncryptionInfo"));
            bytes.AddRange(new byte[32]);
            bytes.AddRange(Encoding.Unicode.GetBytes("EncryptedPackage"));
            bytes.AddRange(new byte[1024]);
            return bytes.ToArray();
        }

        private static void WriteEntry(System.IO.Compression.ZipArchive zip, string name, string content)
        {
            System.IO.Compression.ZipArchiveEntry entry = zip.CreateEntry(name);
            using (Stream s = entry.Open())
            {
                byte[] data = Encoding.UTF8.GetBytes(content);
                s.Write(data, 0, data.Length);
            }
        }

        private static byte[] Build(Action<XlsxWorkbookBuilder> configure)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                using (SpreadsheetDocument doc = SpreadsheetDocument.Create(ms, SpreadsheetDocumentType.Workbook))
                {
                    XlsxWorkbookBuilder builder = new XlsxWorkbookBuilder(doc);
                    configure(builder);
                    builder.Finish();
                }

                return ms.ToArray();
            }
        }
    }
}

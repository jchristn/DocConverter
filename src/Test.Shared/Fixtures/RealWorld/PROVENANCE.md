# Real-world fixture provenance

Files in this folder were produced by software other than DocConverter. They exist to catch what generated fixtures miss:
producer quirks, unusual part layouts, and content that no builder in this repository thought to create.

| Folder | Files | Producer | Notes |
|---|---|---|---|
| `DocumentAtom/` | `sample.csv`, `.docx`, `.html`, `.jpg`, `.json`, `.md`, `.pdf`, `.png`, `.pptx`, `.rtf`, `.txt`, `.xlsx`, `.xml` | `generate_fixtures.py` in the DocumentAtom repository (Python, hand-assembled OOXML zips and a hand-written PDF) | Copied from `DocumentAtom/sdk/test-fixtures`. MIT, same author. Tiny, and structurally minimal: good for "the reader copes with a sparse but valid file". |

Office-authored files (Word, Excel, PowerPoint), LibreOffice exports, Google Docs exports and browser-printed PDFs are
still wanted. Add them to a new subfolder with a row in this table naming the producer and version.

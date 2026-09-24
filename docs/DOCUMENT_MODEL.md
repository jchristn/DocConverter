# Document model

Every conversion goes through one in-memory model. A reader turns the source into a `DocumentModel`; option driven
transforms run on it; a writer turns it into the target. Because every reader and every writer speak the same model,
18 readers and 11 writers give 198 conversions without 198 converters, and a document read once can be written to any
number of targets. The model is public, so you can build documents in code, edit what a reader produced, or write your
own reader or writer against it ([EXTENDING.md](EXTENDING.md)).

The model also has a canonical JSON and XML form. Converting any document to JSON (or XML) and reading it back gives
exactly the same model, which makes the JSON form a safe interchange and storage format.

## DocumentModel

```csharp
public class DocumentModel
{
    public DocumentMetadata Metadata { get; set; }
    public List<Block> Blocks { get; set; }                              // top level blocks in reading order
    public Dictionary<string, BinaryResource> Resources { get; set; }    // images, keyed by id (ordinal)
    public string AddResource(BinaryResource resource);
}
```

`AddResource` stores a resource under its `Id` and returns the id; a resource without an id gets the first free one of
`img1`, `img2` and so on. Images in the text are `ImageBlock` or `ImageInline` elements that point at a resource by id.

`DocumentMetadata` holds `Title`, `Subject`, `Author`, `Keywords` (one comma separated string), `Description`,
`Language` (a tag such as `en-US`), `CreatedUtc` and `ModifiedUtc`. Every member is optional.

`BinaryResource` holds `Id`, `MediaType` (default `application/octet-stream`), `Data`, `FileName`, `PixelWidth` and
`PixelHeight`.

## Blocks

`Block` is the abstract base of everything at block level. Each block can carry an `Id` (an HTML id or bookmark) and
where it came from: `SourcePage` for PDF, `SourceSheet` for XLSX, `SourceSlide` for PPTX.

| Block | Content | Notes |
|---|---|---|
| `HeadingBlock` | `Level`, `Inlines` | Level is clamped to 1 through 6 on assignment. |
| `ParagraphBlock` | `Inlines`, `Alignment` | `Alignment` defaults to `Default`, meaning the target's own default. |
| `ListBlock` | `Kind`, `Start`, `Items` | `Kind` is `Unordered`, `Ordered` or `Task`; `Start` is the first number (default 1). |
| `ListItemBlock` | `Blocks`, `Checked` | Holds paragraphs and nested `ListBlock`s. `Checked` is set for task items only. |
| `TableBlock` | `Rows`, `HeaderRowCount`, `Caption`, `ColumnAlignments` | `ColumnCount` is computed: the widest row, counting column spans. |
| `CodeBlock` | `Text`, `Language` | Lines separated by `\n`. |
| `QuoteBlock` | `Blocks` | |
| `ImageBlock` | `ResourceId`, `AltText`, `Caption`, `Width`, `Height` | Width and height are display sizes in points; null means derive from the pixel size. |
| `SectionBlock` | `Kind`, `Title`, `Blocks` | A page, slide, sheet or generic group (below). |
| `ThematicBreakBlock` | none | A horizontal rule. |
| `PageBreakBlock` | none | Formats without pages ignore it. |

Tables are made of `TableRow` (a list of `Cells`) and `TableCell` (`Blocks`, `ColumnSpan`, `RowSpan`, `IsHeader`). Rows
may be ragged; writers pad them to the widest row. A cell covered by a span from another cell is simply absent, the way
HTML tables work.

## Inlines

`Inline` is the abstract base of content inside headings, paragraphs and links.

| Inline | Content |
|---|---|
| `TextInline` | `Text` and `Style`, a set of `InlineStyleEnum` flags: `Bold`, `Italic`, `Underline`, `Strikethrough`, `Code`, `Superscript`, `Subscript`. |
| `LinkInline` | `Url`, optional `Title`, and `Inlines` for the link text. |
| `ImageInline` | `ResourceId` and `AltText`. |
| `LineBreakInline` | A hard line break. |

Text is stored unescaped. Every writer escapes for its own target, so a paragraph that says `<tag> & *stars*` stays
exactly that text in every format.

## Sections

`SectionBlock` groups blocks. Its `Kind` says what the group is, and writers treat each kind differently: DOCX and HTML
keep them as groups with a title, PPTX starts a slide per `Slide` section, XLSX names a sheet after a `Sheet` section.

| Kind | Produced by | Holds |
|---|---|---|
| `Page` | PDF, when `PdfOptions.PreservePages` is set | One page's blocks. |
| `Slide` | PPTX | One slide. The title placeholder also becomes a level 1 heading inside the section, and the subtitle a level 2 heading. |
| `Sheet` | XLSX | One visible worksheet: title rows above the table as paragraphs, then the table. |
| `Generic` | JSON and XML read as data, DOCX notes | A titled group, for example one JSON object property or one XML element. |

When a section's title repeats its first heading (the PPTX case), writers print it once.

## Invariants

The model is built so readers and writers never have to defend against null:

- Every collection member (`Blocks`, `Inlines`, `Items`, `Rows`, `Cells`, `Resources`, `ColumnAlignments`) is initialized,
  and assigning null stores an empty collection. `Metadata` and string members such as `TextInline.Text`,
  `CodeBlock.Text`, `LinkInline.Url` and `BinaryResource.Data` coalesce null the same way.
- Heading levels are 1 to 6; column and row spans are at least 1; list start and header row count are at least 0. These
  are enforced by the setters, not checked later.
- Every `ResourceId` should resolve to an entry in `Resources`. Built-in readers guarantee it; writers that meet a
  missing resource skip the image or write a placeholder, with a warning, instead of failing.
- A model is not thread safe for mutation. Converting the same model from several threads at once is fine, because the
  write methods copy it before changing anything.

## How the readers map

Each reader fills the model as fully as its format allows. A short map, with the full detail in
[FORMATS.md](FORMATS.md#readers):

- **Markdown and HTML** map almost one to one: headings, paragraphs with inline styles and links, nested lists, tables,
  code with its language, quotes, images (from data URIs; other image URLs stay links).
- **DOCX** maps styles to headings, runs to styled text, numbering to lists, tables with spans, monospace or code styles
  to code blocks, Quote styles to quotes, and images at their position.
- **XLSX** gives one `Sheet` section per visible sheet holding one table over the used range.
- **PPTX** gives one `Slide` section per slide with titles as headings, bullets as lists, tables and pictures.
- **PDF** gives paragraphs in reading order, headings inferred from font size, lists from their markers, ruled tables,
  and images, each block tagged with `SourcePage`.
- **RTF** maps headings, styles, tables, lists, hyperlink fields and pictures.
- **CSV and TSV** give one table.
- **JSON and XML** carrying the canonical marker are read exactly; any other JSON or XML becomes key/value tables,
  record tables, lists and `Generic` sections.
- **Text** gives paragraphs split on blank lines, with line breaks kept.
- **Images** give one `ImageBlock` and its resource.

## Canonical JSON

The JSON writer writes the canonical form and the JSON reader recognizes it by a top level `"docconverter"` property.
Version 1 is the only version; a document with any other version is refused with `DocumentReadException`. Members that
are null are left out.

This is the real output of `docconv convert -i notes.md -o - --to json` for a small Markdown file with front matter, a
heading, a styled paragraph with a link, a list and a table with a right-aligned column:

```json
{
  "docconverter": "1",
  "metadata": {
    "title": "Release notes",
    "author": "Docs Team"
  },
  "blocks": [
    {
      "type": "heading",
      "level": 1,
      "inlines": [
        { "type": "text", "text": "Release 2.4" }
      ]
    },
    {
      "type": "paragraph",
      "inlines": [
        { "type": "text", "text": "The " },
        { "type": "text", "text": "sync engine", "style": [ "Bold" ] },
        { "type": "text", "text": " now retries " },
        { "type": "text", "text": "twice", "style": [ "Italic" ] },
        { "type": "text", "text": ". See " },
        {
          "type": "link",
          "url": "https://example.com/guide",
          "inlines": [ { "type": "text", "text": "the guide" } ]
        },
        { "type": "text", "text": "." }
      ]
    },
    {
      "type": "list",
      "kind": "Unordered",
      "items": [
        { "blocks": [ { "type": "paragraph", "inlines": [ { "type": "text", "text": "Faster uploads" } ] } ] },
        { "blocks": [ { "type": "paragraph", "inlines": [ { "type": "text", "text": "Fewer conflicts" } ] } ] }
      ]
    },
    {
      "type": "table",
      "rows": [
        {
          "cells": [
            { "blocks": [ { "type": "paragraph", "inlines": [ { "type": "text", "text": "Region" } ] } ], "header": true },
            { "blocks": [ { "type": "paragraph", "inlines": [ { "type": "text", "text": "Users" } ] } ], "header": true }
          ]
        },
        {
          "cells": [
            { "blocks": [ { "type": "paragraph", "inlines": [ { "type": "text", "text": "EMEA" } ] } ] },
            { "blocks": [ { "type": "paragraph", "inlines": [ { "type": "text", "text": "1200" } ] } ] }
          ]
        },
        {
          "cells": [
            { "blocks": [ { "type": "paragraph", "inlines": [ { "type": "text", "text": "APAC" } ] } ] },
            { "blocks": [ { "type": "paragraph", "inlines": [ { "type": "text", "text": "950" } ] } ] }
          ]
        }
      ],
      "headerRowCount": 1,
      "columnAlignments": [ "Default", "Right" ]
    }
  ],
  "resources": []
}
```

The writer indents every array element on its own line; the layout above is condensed for reading, the content is
identical.

### Fields

`CanonicalDocument` (the root):

| Field | Type | Meaning |
|---|---|---|
| `docconverter` | string | Format marker and version, `"1"`. |
| `metadata` | object | `title`, `subject`, `author`, `keywords`, `description`, `language`, `created`, `modified` (ISO 8601, UTC). Omitted when metadata is excluded. |
| `blocks` | array | Top level blocks. |
| `resources` | array | `id`, `mediaType`, `fileName`, `pixelWidth`, `pixelHeight`, `size` (bytes) and `data` (base64; omitted when binary data is excluded). |

`CanonicalBlock` has a `type` and the members that type uses:

| `type` | Members |
|---|---|
| `heading` | `level`, `inlines` |
| `paragraph` | `inlines`, `alignment` (`Left`, `Center`, `Right`, `Justify`; omitted for the default) |
| `list` | `kind` (`Unordered`, `Ordered`, `Task`), `start` (ordered lists), `items`: each `{ "blocks": [...], "checked": true or false }` |
| `table` | `rows`: each `{ "cells": [...] }`, where a cell is `{ "blocks": [...], "colSpan", "rowSpan", "header" }` with spans and header omitted when 1 or false; `headerRowCount`, `caption`, `columnAlignments` |
| `code` | `language`, `text` |
| `quote` | `blocks` |
| `image` | `resourceId`, `altText`, `caption`, `width`, `height` |
| `section` | `kind` (`Generic`, `Page`, `Slide`, `Sheet`), `title`, `blocks` |
| `thematicBreak`, `pageBreak` | none |

Every block may also carry `id`, `sourcePage`, `sourceSheet` and `sourceSlide`.

`CanonicalInline` has a `type`:

| `type` | Members |
|---|---|
| `text` | `text`, `style` (array of `Bold`, `Italic`, `Underline`, `Strikethrough`, `Code`, `Superscript`, `Subscript`; omitted when plain) |
| `link` | `url`, `title`, `inlines` |
| `image` | `resourceId`, `altText` |
| `lineBreak` | none |

An unknown `type` or style name, or invalid base64 in `data`, is refused with `DocumentReadException`.

## Canonical XML

The XML writer writes the same content with a `<docconverter version="1">` root, and the XML reader recognizes that
root. Block and inline members become attributes; nested content becomes child elements (`inlines`, `blocks`, `items`,
`rows`, `cells`, `text` for code); resource data is the text of each `<resource>`. The same document as above:

```xml
<?xml version="1.0" encoding="utf-8"?>
<docconverter version="1">
  <metadata>
    <title>Release notes</title>
    <author>Docs Team</author>
  </metadata>
  <blocks>
    <block type="heading" level="1">
      <inlines>
        <inline type="text" xml:space="preserve">Release 2.4</inline>
      </inlines>
    </block>
    <block type="paragraph">
      <inlines>
        <inline type="text" xml:space="preserve">The </inline>
        <inline type="text" style="Bold" xml:space="preserve">sync engine</inline>
        <inline type="text" xml:space="preserve"> now retries </inline>
        <inline type="text" style="Italic" xml:space="preserve">twice</inline>
        <inline type="text" xml:space="preserve">. See </inline>
        <inline type="link" url="https://example.com/guide">
          <inlines>
            <inline type="text" xml:space="preserve">the guide</inline>
          </inlines>
        </inline>
        <inline type="text" xml:space="preserve">.</inline>
      </inlines>
    </block>
    <block type="list" kind="Unordered">
      <items>
        <item>
          <blocks>
            <block type="paragraph">
              <inlines>
                <inline type="text" xml:space="preserve">Faster uploads</inline>
              </inlines>
            </block>
          </blocks>
        </item>
        <item>
          <blocks>
            <block type="paragraph">
              <inlines>
                <inline type="text" xml:space="preserve">Fewer conflicts</inline>
              </inlines>
            </block>
          </blocks>
        </item>
      </items>
    </block>
    <block type="table" headerRowCount="1" columnAlignments="Default,Right">
      <rows>
        <row>
          <cell header="true">
            <blocks>
              <block type="paragraph">
                <inlines>
                  <inline type="text" xml:space="preserve">Region</inline>
                </inlines>
              </block>
            </blocks>
          </cell>
          <cell header="true">
            <blocks>
              <block type="paragraph">
                <inlines>
                  <inline type="text" xml:space="preserve">Users</inline>
                </inlines>
              </block>
            </blocks>
          </cell>
        </row>
        <row>
          <cell>
            <blocks>
              <block type="paragraph">
                <inlines>
                  <inline type="text" xml:space="preserve">EMEA</inline>
                </inlines>
              </block>
            </blocks>
          </cell>
          <cell>
            <blocks>
              <block type="paragraph">
                <inlines>
                  <inline type="text" xml:space="preserve">1200</inline>
                </inlines>
              </block>
            </blocks>
          </cell>
        </row>
        <row>
          <cell>
            <blocks>
              <block type="paragraph">
                <inlines>
                  <inline type="text" xml:space="preserve">APAC</inline>
                </inlines>
              </block>
            </blocks>
          </cell>
          <cell>
            <blocks>
              <block type="paragraph">
                <inlines>
                  <inline type="text" xml:space="preserve">950</inline>
                </inlines>
              </block>
            </blocks>
          </cell>
        </row>
      </rows>
    </block>
  </blocks>
  <resources />
</docconverter>
```

Style flags and column alignments are comma separated attribute values. Characters XML 1.0 cannot represent (most
control characters and unpaired surrogates) are removed on writing. The XML declaration names
`ConversionOptions.OutputEncoding`. Reading prohibits DTDs and external entities.

## Round trips

JSON and XML round trips are lossless by contract: `CanonicalMapper.FromDto(CanonicalMapper.ToDto(model))` equals the
model, and a document written to canonical JSON or XML and read back is identical to what was written. The test suite
checks this for every source format, and checks that repeating the round trip changes nothing. One nuance: the write
methods apply the documented option transforms first (for example `IncludeImages = false`, and clearing bold that is
redundant on headings and header cells), so compare against what was written rather than against a raw read.

In code, the canonical form is available without going through bytes:

```csharp
CanonicalDocument dto = CanonicalMapper.ToDto(model, includeMetadata: true, includeBinary: false);
DocumentModel copy = CanonicalMapper.Clone(model);    // deep copy, resource bytes included
```

# Cadmus.Export

This library contains core components for Cadmus export processes. These are essentially of two types, covered by two top-level classes, `CadmusPreviewer` and `ItemComposer`.

These classes operate independently and serve different purposes: `ItemComposer` is designed for exporting data in bulk, while `CadmusPreviewer` is tailored for interactive previews in a UI.

Both classes rely on `CadmusRenderingFactory` to obtain components, and use `CadmusRendererContext` to manage contextual metadata during rendering.

## Factory

The Cadmus rendering factory relies on a JSON configuration document with these sections:

- named components (via keys):
  - **ContextSuppliers**: list of renderer context suppliers, each named with a key, and having its component ID and eventual options. The key is an arbitrary string, used in the scope of the configuration to reference each filter from other sections.
  - **TextTreeFilters**: list of text tree filters, each named with a key, and having its component ID and eventual options. The key is an arbitrary string, used in the scope of the configuration to reference each filter from other sections.
  - **TextFilters**: list of text filters, each named with a key, and having its component ID and eventual options. The key is an arbitrary string, used in the scope of the configuration to reference each filter from other sections.
  - **JsonRenderers**: list of JSON renderers, each named with a key, and having its component ID and eventual options. The key corresponds to the part type ID, eventually followed by `|` and its role ID in the case of a layer part. This allows mapping each part type to a specific renderer ID. This key is used in the scope of the configuration to reference each filter from other sections. Under options, any renderer can have a `FilterKeys` property which is an array of filter keys, representing the filters used by that renderer, to be applied in the specified order.
  - **TextPartFlatteners**: list of text part flatteners, each named with a key, and having its component ID and eventual options. The key is an arbitrary string, used in the scope of the configuration to reference each filter from other sections.
  - **TextTreeRenderers**: list of text tree renderers, each named with a key, and having its component ID and eventual options. The key is an arbitrary string, used in the scope of the configuration to reference each filter from other sections.
  - **ItemComposers**: list of item composers, each named with a key, and having its component ID and eventual options. The key is an arbitrary string, not used elsewhere in the context of the configuration. It is used as an argument for UI which process data export. Each composer can have among its options a `TextPartFlattenerKey`, a `TextTreeFilterKeys`, a `TextTreeRendererKey` and a `JsonRendererKeys`, referencing the corresponding components by their key.
- single components:
  - **ItemIdCollector**: a single item ID collector to use when required. It has the component ID, and eventual options.

## UI

`CadmusPreviewer` in the editor API is used to:

- render a **part** (`RenderPart`, `RenderPartJson`): this uses a repository to get the part's content (JSON), asks the factory to provide a JSON renderer from the part's type ID, and uses it to build the output string. If there is no renderer, it just returns an empty string.

- render a **fragment** (`RenderFragment`, `RenderFragmentJson`): this uses a repository to get the layer part and its item, and builds a key with type ID plus `:` plus role ID (which identifies the fragment's type). Then it asks the factory to provide a JSON renderer from this key, and uses it to build the output string from the fragment's JSON at the specified index. If there is no renderer, it just returns an empty string.

>⚠️ Note that in previous versions `|` was used as the separator, so you will need to change your preview configuration.

- get **text segments** with fragment links (`BuildTextSegments`): this uses a repository to get the base text part's content (JSON); it then gets the flattener for its type ID, loads the text part and its item, flattens the layers (or a selection of them, which is got as a parameter), gets the corresponding text ranges, and builds a linear tree from them. Finally, it applies a block linear text tree filter to it to split nodes at every occurrence of a LF character, and collects nodes payloads into an array of segments, which is the result.

As an example, consider a text like this (here `|`=LF and layer fragments are represented by letters on lines below the text):

```txt
0123456789-1234567
que bixit|annos XX
..O............... 1.1@3
....O............. 1.2@1
....CCCCCCCCCCC... 1.2-2.1
................CC 2.2
```

This generates these segments: each has a text and a source range with fragment IDs:

- `qu`
- `e`:
  - `it.vedph.token-text-layer:fr.it.vedph.orthography@0` (E for AE)
- `_`
- `b`:
  - `it.vedph.token-text-layer:fr.it.vedph.orthography@1` (B for V)
  - `it.vedph.token-text-layer:fr.it.vedph.comment@0` (comment on BIXIT ANNOS)
- `ixit`
  - `it.vedph.token-text-layer:fr.it.vedph.comment@0` (comment on BIXIT ANNOS)
- `annos`:
  - `it.vedph.token-text-layer:fr.it.vedph.comment@0` (comment on BIXIT ANNOS)
- `_`
- `XX`:
  - `it.vedph.token-text-layer:fr.it.vedph.comment@1` (comment on XX)

### Sample Configuration

Sample UI preview configuration:

```json
{
  "TextFilters": [
    {
      "Keys": "markdown",
      "Id": "it.vedph.text-filter.str.markdown",
      "Options": {
        "MarkdownOpen": "<_md>",
        "MarkdownClose": "</_md>",
        "Format": "html"
      }
    },
    {
      "Keys": "token-extractor",
      "Id": "it.vedph.text-filter.str.mongo-token-extractor",
      "Options": {
        "LocationPattern": "<location>([^<]+)</location>",
        "TextTemplate": "<span class=\"apparatus-lemma\">{text}</span>",
        "TextCutting": true,
        "Mode": 3,
        "Limit": 80,
        "MinusLimit": 5,
        "PlusLimit": 5
      }
    },
    {
      "Keys": "iso639-3",
      "Id": "it.vedph.text-filter.str.iso639"
    }
  ],
  "JsonRenderers": [
    {
      "Keys": "it.vedph.note",
      "Id": "it.vedph.json-renderer.xslt",
      "Options": {
        "QuoteStripping ": true,
        "Xslt": "<?xml version=\"1.0\" encoding=\"UTF-8\"?><xsl:stylesheet xmlns:xsl=\"http://www.w3.org/1999/XSL/Transform\" version=\"1.0\"><xsl:output media-type=\"text/html\" method=\"html\" omit-xml-declaration=\"yes\" encoding=\"UTF-8\"/><xsl:template match=\"tag[normalize-space(.)]\"><div class=\"pv-muted\"><xsl:value-of select=\".\"/></div></xsl:template><xsl:template match=\"text\"><div class=\"note-text\"><_md><xsl:value-of select=\".\"/></_md></div></xsl:template><xsl:template match=\"root\"><xsl:apply-templates/></xsl:template><xsl:template match=\"*\"/></xsl:stylesheet>",
        "FilterKeys": [ "markdown" ]
      }
    },
    {
      "Keys": "it.vedph.token-text-layer:fr.it.vedph.apparatus",
      "Id": "it.vedph.json-renderer.xslt",
      "Options": {
        "FilterKeys": [ "token-extractor" ],
        "Xslt": "<?xml version=\"1.0\" encoding=\"UTF-8\"?><xsl:stylesheet xmlns:xsl=\"http://www.w3.org/1999/XSL/Transform\" xmlns:xs=\"http://www.w3.org/2001/XMLSchema\" exclude-result-prefixes=\"xs\" version=\"1.0\"><xsl:output media-type=\"text/html\" method=\"html\" omit-xml-declaration=\"yes\" encoding=\"UTF-8\"/><xsl:template match=\"lemma\"><span class=\"apparatus-lemma\"><xsl:value-of select=\".\"/></span></xsl:template><xsl:template match=\"location\"><location><xsl:value-of select=\".\"/></location></xsl:template><xsl:template match=\"witnesses\"><span class=\"apparatus-w-value\"><xsl:value-of select=\"value\"/></span><xsl:if test=\"note\"><span class=\"apparatus-w-note\"><xsl:text> </xsl:text><xsl:value-of select=\"note\"/><xsl:text> </xsl:text></span></xsl:if></xsl:template><xsl:template match=\"authors\"><xsl:text> </xsl:text><span class=\"apparatus-a-value\"><xsl:value-of select=\"value\"/></span><xsl:if test=\"note\"><xsl:text> </xsl:text><span class=\"apparatus-a-note\"><xsl:value-of select=\"note\"/></span></xsl:if><xsl:text> </xsl:text></xsl:template><xsl:template match=\"entries\"><xsl:variable name=\"nr\"><xsl:number/></xsl:variable><xsl:if test=\"$nr &gt; 1\"><span class=\"apparatus-sep\">| </span></xsl:if><xsl:if test=\"tag\"><span class=\"apparatus-tag\"><xsl:value-of select=\"tag\"/></span><xsl:text> </xsl:text></xsl:if><xsl:if test=\"subrange\"><span class=\"apparatus-subrange\"><xsl:value-of select=\"subrange\"/></span><xsl:text> </xsl:text></xsl:if><xsl:if test=\"string-length(value) &gt; 0\"><span class=\"apparatus-value\"><xsl:value-of select=\"value\"/></span><xsl:text> </xsl:text></xsl:if><xsl:choose><xsl:when test=\"type = 0\"><xsl:if test=\"string-length(value) = 0\"><span class=\"apparatus-type\">del. </span></xsl:if></xsl:when><xsl:when test=\"type = 1\"><span class=\"apparatus-type\">ante lemma </span></xsl:when><xsl:when test=\"type = 2\"><span class=\"apparatus-type\">post lemma </span></xsl:when></xsl:choose><xsl:if test=\"note\"><span class=\"apparatus-note\"><xsl:value-of select=\"note\"/></span><xsl:text> </xsl:text></xsl:if><xsl:apply-templates/></xsl:template><xsl:template match=\"root\"><xsl:apply-templates/></xsl:template><xsl:template match=\"*\"/></xsl:stylesheet>"
      }
    },
    {
      "Keys": "it.vedph.token-text-layer:fr.it.vedph.comment",
      "Id": "it.vedph.json-renderer.xslt",
      "Options": {
        "WrappedEntryNames": {
          "categories": "category",
          "references": "reference",
          "keywords": "keyword",
          "externalIds": "externalId"
        },
        "Xslt": "<?xml version=\"1.0\" encoding=\"UTF-8\"?><xsl:stylesheet xmlns:xsl=\"http://www.w3.org/1999/XSL/Transform\" xmlns:xs=\"http://www.w3.org/2001/XMLSchema\" exclude-result-prefixes=\"xs\" version=\"1.0\"><xsl:output media-type=\"text/html\" method=\"html\" omit-xml-declaration=\"yes\" encoding=\"UTF-8\"/><xsl:template match=\"*[not(*) and not(normalize-space())]\"></xsl:template><xsl:template name=\"build-link\"><xsl:param name=\"val\"/><xsl:choose><xsl:when test=\"starts-with($val, 'http')\"><xsl:element name=\"a\"><xsl:attribute name=\"href\"><xsl:value-of select=\"$val\"/></xsl:attribute><xsl:attribute name=\"target\">_blank</xsl:attribute><xsl:value-of select=\"$val\"/></xsl:element></xsl:when><xsl:otherwise><xsl:value-of select=\"$val\"/></xsl:otherwise></xsl:choose></xsl:template><xsl:template match=\"reference\"><li><xsl:if test=\"type[normalize-space(.)]\"><span class=\"comment-ref-y\"><xsl:value-of select=\"type\"/></span></xsl:if><xsl:if test=\"tag[normalize-space(.)]\"><span class=\"comment-ref-t\"><xsl:value-of select=\"tag\"/></span></xsl:if><xsl:if test=\"citation\"><span class=\"comment-ref-c\"><xsl:call-template name=\"build-link\"><xsl:with-param name=\"val\" select=\"citation\"></xsl:with-param></xsl:call-template></span></xsl:if><xsl:if test=\"note[normalize-space(.)]\"><xsl:text></xsl:text><span class=\"comment-ref-n\"><xsl:value-of select=\"note\"/></span></xsl:if></li></xsl:template><xsl:template match=\"root\"><div class=\"comment\"><xsl:if test=\"tag[normalize-space(.)]\"><div class=\"comment-tag\"><xsl:value-of select=\"tag\"/></div></xsl:if><xsl:if test=\"categories/category\"><div class=\"pv-flex-row comment-categories\"><xsl:for-each select=\"categories/category\"><div class=\"comment-category\"><xsl:value-of select=\".\"/></div></xsl:for-each></div></xsl:if><xsl:if test=\"text\"><div class=\"comment-text\"><_md><xsl:value-of select=\"text\"/></_md></div></xsl:if><xsl:if test=\"keywords/keyword\"><ul class=\"comment-keywords\"><xsl:for-each select=\"keywords/keyword\"><xsl:sort select=\"indexId\"/><xsl:sort select=\"language\"/><xsl:sort select=\"value\"/><li><xsl:if test=\"indexId[normalize-space(.)]\"><span class=\"comment-kw-x\"><xsl:value-of select=\"indexId\"/></span></xsl:if><span class=\"comment-kw-l\">^^<xsl:value-of select=\"language\"/></span><span class=\"comment-kw-v\"><xsl:value-of select=\"value\"/></span></li></xsl:for-each></ul></xsl:if><xsl:if test=\"references/*\"><div class=\"comment-hdr\">references</div><ol class=\"comment-references\"><xsl:apply-templates select=\"references/reference\"/></ol></xsl:if><xsl:if test=\"externalIds/*\"><div class=\"comment-hdr\">identifiers</div><ul class=\"comment-ids\"><xsl:for-each select=\"externalIds/externalId\"><li><xsl:if test=\"tag[normalize-space(.)]\"><span class=\"comment-id-t\"><xsl:value-of select=\"tag\"/></span></xsl:if><xsl:if test=\"scope[normalize-space(.)]\"><span class=\"comment-id-s\"><xsl:value-of select=\"scope\"/></span></xsl:if><span class=\"comment-id-v\"><xsl:call-template name=\"build-link\"><xsl:with-param name=\"val\" select=\"value\"/></xsl:call-template></span><xsl:if test=\"assertion/*\"><div class=\"comment-assertion\"><xsl:if test=\"assertion/tag\"><span class=\"comment-id-t\"><xsl:value-of select=\"assertion/tag\"/></span></xsl:if><xsl:if test=\"assertion/rank\"><xsl:text></xsl:text><span class=\"comment-id-r\">R<xsl:value-of select=\"assertion/rank\"/></span></xsl:if><xsl:if test=\"assertion/note\"><xsl:text></xsl:text><div class=\"comment-id-n\"><xsl:value-of select=\"assertion/note\"/></div></xsl:if><xsl:if test=\"assertion/references\"><ol class=\"comment-assertion-refs\"><xsl:apply-templates select=\"assertion/references/reference\"/></ol></xsl:if></div></xsl:if></li></xsl:for-each></ul></xsl:if></div></xsl:template><xsl:template match=\"*\"/></xsl:stylesheet>",
        "FilterKeys": [ "markdown", "iso639-3" ]
      }
    }
  ],
  "TextPartFlatteners": [
    {
      "Keys": "it.vedph.token-text",
      "Id": "it.vedph.text-flattener.token"
    }
  ]
}
```

Explanation:

- **text filters**: text filters, each having its arbitrarily defined key, used to reference it in other sections of the configuration document. Here we have:
  - `markdown`: Markdown rendering filter configured for an HTML output.
  - `token-extractor`: token extractor using MongoDB.
  - `iso639-3`: ISO639-3 code resolver.
- **JSON renderers**: JSON renderers, each having its arbitrarily defined key, used to reference it in other sections of the configuration document. Here we have:
  - `it.vedph.note`: XSLT-based renderer for the general [note part](https://github.com/vedph/cadmus-general/blob/master/docs/note.md). This uses the Markdown text filter.
  - `it.vedph.token-text-layer:fr.it.vedph.apparatus`: XSLT-based renderer for the [apparatus fragment](https://github.com/vedph/cadmus-philology/blob/master/docs/fr.apparatus.md). This uses the token extractor filter to get lemmata.
  - `it.vedph.token-text-layer:fr.it.vedph.comment`: XSLT-based renderer for the [comment fragment](https://github.com/vedph/cadmus-general/blob/master/docs/fr.comment.md). This uses the Markdown filter for the comment's text and the ISO639 filter for keyword languages.
- **text part flatteners**: here we just use a single flattener for the [token-based text part](https://github.com/vedph/cadmus-general/blob/master/docs/token-text.md) (`it.vedph.token-text`).

## Bulk

`ItemComposer` in the export CLI tool is used to export text and its annotations. The render items command:

1. loads the JSON rendering configuration.
2. instantiates factory and repository.
3. ask the factory to create an `IItemComposer` instance and an `IItemIdCollector` instance.
4. uses the ID collector to get the IDs of the items to render. For each ID, it fetches the item using the repository, and then uses the composer to compose its rendition, saving it somewhere according to the configuration.

The **composer**, which is an abstract base class, gets an item and renders it as follows:

1. stores some item's metadata into its rendering context (ordinal number, ID, title, facet ID, flags, group ID).
2. optionally augments the context metadata using all the configured context suppliers.
3. handles the group change if any.
4. calls a derived class `Compose` method to do the real rendering.

The implementation of `Compose` depends on the output being generated. For instance, there are:

- `FSPlainTextItemComposer`: export plain text documents into a single file or one file per group. This builds a text tree from the item's base text part and its selected layer parts, uses its text tree renderer to render the tree into a string, and writes it. According to its configuration, it can also output a header and a footer before and after each rendered item.
- `TEIItemComposer`: simple TEI composer. This is an abstract class, and just builds a text tree and renders text from it. Implementations provide more infrastructure to save the result.
  - `FSTeiItemComposer`: file-based TEI composer.
- `TeiOffItemComposer`: TEI standoff composer. This is an abstract class, and builds a text tree and renders txt from it. It then uses its JSON renderers dictionary (keyed by layer type part ID + `:` + layer part role ID) to render each layer and write it to some output stream (keyed by the layer part role ID).
  - `FSTeiOffItemComposer`: file-based standoff TEI composer.

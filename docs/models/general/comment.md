# Comment

A general purpose free-text comment with some attached metadata.

🔑 `it.vedph.comment`

- tag (`string` 📚 `comment-tags`)
- text* (`string`, MD)
- references (🧱 [DocReference[]](https://github.com/vedph/cadmus-bricks/blob/master/docs/doc-reference.md)):
  - type (`string` 📚 `doc-reference-types`)
  - tag (`string` 📚 `doc-reference-tags`)
  - citation (`string`)
  - note (`string`)
- links (🧱 [AssertedCompositeId[]](https://github.com/vedph/cadmus-bricks/blob/master/docs/asserted-composite-id.md)):
  - target (`PinTarget`):
    - gid\* (`string`)
    - label\* (`string`)
    - itemId (`string`)
    - partId (`string`)
    - partTypeId (`string`)
    - roleId (`string`)
    - name (`string`)
    - value (`string`)
  - scope (`string` 📚 `comment-id-scopes`)
  - features (`string[]` 📚 `asserted-id-features`, hierarchical)
  - note (`string`)
  - tag (`string` 📚 `comment-id-tags`)
  - assertion (🧱 [Assertion](https://github.com/vedph/cadmus-bricks/blob/master/docs/assertion.md)):
    - tag (`string` 📚 `assertion-tags`)
    - rank (`short`)
    - references (🧱 [DocReference[]](https://github.com/vedph/cadmus-bricks/blob/master/docs/doc-reference.md))
- categories (`string[]` 📚 `comment-categories`)
- keywords ([IndexKeyword[]](index-keywords.md)):
  - language* (`string` 📚 `comment-keyword-languages`)
  - value* (`string`)
  - indexId (`string` 📚 `comment-keyword-indexes`)
  - note (`string`)
  - tag (`string` 📚 `comment-keyword-tags`)

>⚠️ Note: in versions before 5, `links` was `externalIds` of type `AssertedId[]`.

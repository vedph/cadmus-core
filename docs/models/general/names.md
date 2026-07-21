# Names

List of generic proper names.

🔑 `it.vedph.names`

- names (`AssertedProperName[]`):
  - language (`string` 📚 `name-languages`)
  - tag (`string` 📚 `name-tags`)
  - pieces (🧱 [ProperNamePiece[]](https://github.com/vedph/cadmus-bricks/blob/master/docs/proper-name-piece.md)):
    - type (`string` 📚 `name-piece-types`)
    - value (`string`)
  - assertion (🧱 [Assertion](https://github.com/vedph/cadmus-bricks/blob/master/docs/assertion.md)):
    - tag (`string` 📚 `assertion-tags`)
    - rank (`short`)
    - references (🧱 [DocReference[]](https://github.com/vedph/cadmus-bricks/blob/master/docs/doc-reference.md)):
      - type (`string` 📚 `doc-reference-types`)
      - tag (`string` 📚 `doc-reference-tags`)
      - citation (`string`)
      - note (`string`)

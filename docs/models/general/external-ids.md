# External IDs

External identifiers.

🔑 `it.vedph.external-ids`

- ids (🧱 [AssertedId[]](https://github.com/vedph/cadmus-bricks/blob/master/docs/asserted-id.md)):
  - tag (`string` 📚 `external-id-tags`)
  - value\* (`string`)
  - label (`string`)
  - scope (`string` 📚 `external-id-scopes`)
  - assertion (🧱 [Assertion](https://github.com/vedph/cadmus-bricks/blob/master/docs/assertion.md)):
    - tag (`string` 📚 `assertion-tags`)
    - rank\* (`string`)
    - note (`string`)
    - references (🧱 [DocReference[]](https://github.com/vedph/cadmus-bricks/blob/master/docs/doc-reference.md)):
      - type (`string` 📚 `doc-reference-types`)
      - tag (`string` 📚 `doc-reference-tags`)
      - citation (`string`)
      - note (`string`)

# Pin-Links

Data pin based links. This part is used to collect any number of pin-based dynamic lookup references, so that you can easily connect an item to one or more items via pins targeting any of its parts.

🔑 `it.vedph.pin-links`

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
  - scope (`string` 📚 `pin-link-scopes`)
  - tag (`string` 📚 `pin-link-tags`)
  - features (`string[]` 📚 `asserted-id-features`, hierarchical)
  - note (`string`)
  - assertion (🧱 [Assertion](https://github.com/vedph/cadmus-bricks/blob/master/docs/assertion.md)):
    - tag (`string` 📚 `pin-link-assertion-tags`)
    - rank (`short`)
    - references (🧱 [DocReference[]](https://github.com/vedph/cadmus-bricks/blob/master/docs/doc-reference.md)): 📚 `pin-link-docref-types`, `pin-link-docref-tags`.

>⚠ Note: in versions before 5, `links` was of type `AssertedId[]`.

Additionally, `pin-link-settings` can be used for UI settings, e.g.:

```json
{
  "id": "pin-link-settings@en",
  "entries": [
    {
      "id": "switch-mode",
      "value": "true"
    }
  ]
}
```

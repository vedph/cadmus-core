# Flags

A set of generic binary features (flags), possibly having a note for each flag.

🔑 `it.vedph.flags`

- flags (`string[]` 📚 `flags`)
- notes (dictionary of string keys and values, keys being the flags)

Note that this part is role-dependent and it requires:

- the `flags` thesaurus.
- settings for notes, if any, e.g.:

```json
  "settings": {
    "it.vedph.flags": {
      "definitions": [
        {
          "key": "alpha",
          "label": "alpha",
          "markdown": true,
          "maxLength": 100
        },
        {
          "key": "beta",
          "label": "beta",
          "maxLength": 100
        }
      ]
    }
  }
```

For each flag you want to be optionally annotated, you must enter its entry ID as the definition key, and the details about its note model (label, whether it's Markdown or plain text, and max length).

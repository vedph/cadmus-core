# District Location

A district-based hierarchical location.

🔑 `it.vedph.district-location`

- DistrictLocationPart (🔑 `it.vedph.district-location`):
  - place\* (🧱 [ProperName](https://github.com/vedph/cadmus-bricks/blob/master/docs/proper-name.md)):
    - language (`string`, 📚 `district-name-languages`)
    - tag (`string`)
    - pieces (🧱 [ProperNamePiece[]](https://github.com/vedph/cadmus-bricks/blob/master/docs/proper-name-piece.md), 📚 `district-name-piece-types`, providing 3 levels: area, sestriere, location):
      - type\* (`string`)
      - value\* (`string`)
  - note (`string` 5000)

>Levels are provided via a hierarchical-like thesaurus, where each component is defined by a simple ID with any number of children entries, each with a composite ID, e.g.:

```json
{
  "id": "district-name-piece-types@en",
  "entries": [
    {
      "id": "p*",
      "value": "provincia"
    },
    {
      "id": "c*",
      "value": "città"
    },
    {
      "id": "a*",
      "value": "area"
    },
    {
      "id": "a.cr",
      "value": "Cannareggio"
    },
    {
      "id": "a.cs",
      "value": "Castello"
    },
    {
      "id": "a.dd",
      "value": "Dorsoduro"
    },
    {
      "id": "a.sm",
      "value": "San Marco"
    },
    {
      "id": "a.sp",
      "value": "San Polo"
    },
    {
      "id": "a.sc",
      "value": "Santa Croce"
    },
    {
      "id": "l*",
      "value": "località"
    },
    {
      "id": "s*",
      "value": "struttura"
    },
    {
      "id": "_order",
      "value": "p c a l s"
    }
  ]
},
```

>Here, the asterisk suffix means a parent entry, while entries with children have their values composed by the parent entry ID without the asterisk, plus dot and another ID, like `a.cr`. Finally, the `_order` entry is used to define the hierarchical order of components.

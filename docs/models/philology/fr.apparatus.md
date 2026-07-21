# Apparatus Fragment

Generic critical apparatus.

- location (`string`)
- tag (`string` 📚 !`apparatus-tags`)
- entries (`ApparatusEntry[]`):
  - type\* (int): 0=replacement, 1=addition before, 2=addition after, 3=note.
  - subrange (`string`)
  - tag (`string`)
  - value (`string`)
  - normValue (`string`)
  - isAccepted (`boolean`)
  - groupId (`string`)
  - witnesses (`AnnotatedValue[]` 📚 `apparatus-witnesses`):
    - value\* (`string`)
    - note (`string`)
  - authors (`LocAnnotatedValue[]` 📚 `apparatus-authors`)
    - value\* (`string`)
    - note (`string`)
    - tag (`string` 📚 !`apparatus-author-tags`)
    - location (`string`)
  - note (`string`)

Additional thesaurus:

- 📚 !`author-works`: a hierarchical thesaurus used as an alternative entry method for author/work pairs.

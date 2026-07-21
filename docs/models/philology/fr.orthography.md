# Orthography Fragment

Any relevant deviation from a standard orthography.

🔑 `fr.it.vedph.orthography`

- `location` (`string`)
- `reference` (`string`): the reference form for the word linked to this fragment. If you are using standard orthography in the base text, this is the attested form; otherwise, it is the standard orthography form.
- `language` (`string` 📚 `orthography-languages`)
- `tags` (`string[]` 📚 `orthography-tags`, hierarchical)
- `note` (`string`)
- `operations` (`string[]`): operations are strings using some DSL. Older versions of this fragment used `MspOperation`. Newer versions use `EditOperation` and its derived classes. As each operation can have 0 or more tags, these usually come from a hierarchical thesaurus 📚 `orthography-op-tags`.
- `isTextTarget` (`boolean`): true if the base text is the target of operations; false if the reference form is the target. This determines the source and target forms used by operations.

>Note: before version 11.0.1, `isTextTarget` was not present and `reference` was named `standard`.

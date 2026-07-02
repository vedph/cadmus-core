# Cadmus.Export.ML

This library contains components for Cadmus bulk export processes using markup languages (ML) such as TEI, HTML, etc.

- JSON renderers:
  - `MLJsonRenderer`: base class for JSON renderers targeting markup languages.
  - `TeiOffApparatusJsonRenderer`: works in tandem with `TeiOffLinearTextTreeRenderer` to render the fragments of a layer part in standoff TEI.

- item composers:
  - `TEIItemComposer`: base class for TEI item composers.
    - `FSTeiItemComposer`: file-system TEI item composer.
  - `TeiOffItemComposer`: standoff TEI item composer.
    - `FSTeiOffItemComposer`: file-system standoff TEI item composer.

- text tree renderers:
  - `TeiAppLinearTextTreeRenderer`: renders a TEI linear text tree with a single layer representing its apparatus.
  - `TeiAppParallelTextTreeRenderer`: renders a TEI linear text tree with parallel segmentation.
  - `TeiOffLinearTextTreeRenderer`: renders the base text from a linear tree into a TEI segmented text using `seg`, each with its mapped ID, so that it can be targeted by annotations.

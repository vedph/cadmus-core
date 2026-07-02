# Proteus.Rendering

## Overview

The `Proteus.Rendering` library is a collection of rendering utilities and components designed to leverage the Proteus framework to render some data into text (e.g. plain text, HTML, XML, etc.).

Currently this is being developed to provide all the generic, reusable models and logic for rendering data into text, in order to serve both GVE and Cadmus (`Cadmus.Migration` solution) export subsystems.

>The code in this library is mostly derived from `Cadmus.Migration`, with the necessary changes to make it reusable and generic. When the library is stable, it will be moved to the `Proteus` solution.

Text rendering essentially is a 4-steps process:

1. **build tree**: starting from your specific data source (an item in Cadmus, a snapshot in GVE) build a linear (=single branch) tree representing the text with some annotations. In this tree, each text segment is a node, having any number of name=value pairs, named _features_. Nodes are stacked so that each following segment is a child node of the previous one. The node payload type is `ExportedSegment`, generic enough to be used both in Cadmus and in GVE.
2. **filter tree**: variously filter this tree using text tree filters, adjusting the tree structure to the desired output. So, this starts from a linear tree, but it can lead to a multiple-branches tree.
3. **render output**: render the text tree into some output format, like XML.
4. **filter output**: optionally filter the output using text filters.

This generic library contains shared components:

- text tree filters:
  - `ITextTreeFilter`: interface for text tree filters.
  - `CompositeTextTreeFilter`: wrapper for an inner filter.
  - `LinearMergeTextTreeFilter`: merger for a linear text tree.

- text tree renderers:
  - `IRendererContext` interface for the data context of a renderer.
  - `ITextTreeRenderer`: interface for text tree renderers.
  - `TextTreeRenderer<HandledType>`: base class for text tree renderers.
  - `GroupTextTreeRenderer<HandledType>`: base class for text tree renderers with grouping.
  - `CompositeTextTreeRenderer<HandledType>`: wrapper for an inner text tree renderer.

- helpers:
  - `IdMap`: bidirectional IDs mapping. This is essentially used to get unique numeric IDs for each string-based IDs during rendition.

>Composite components implement interface `IHasCompositeComponent`.

## GVE Export

The general flow for exporting GVE as covered by this library is summarized here. The main components used for GVE rendering currently are:

1. `GveTextTreeBuilder`: builds a linear text tree from a GVE chain.
2. `LinearMergeTextTreeFilter`: merges nodes in a linear text tree.
3. `GveSabaTextTreeRenderer`: renders a linear text tree into Saba-like XML. This renderer covers a single text version, encoded in `rdg`/`lem`.
4. `GveSabaCompositeTextTreeRenderer`: a composite renderer using `GveSabaTextTreeRenderer` for each sub-tree. This renders the root `app` element with a `rdg`/`lem` child for each text version.

### 1. Text Tree Builder

This first stage is the only one which is specific to GVE. Once it has been executed, the data is in a format which can be used by any text tree renderer, and the rest of the pipeline is generic and reusable. The same approach is used by the Cadmus export subsystem.

A `GveTextTreeBuilder` is used to build a linear text tree, representing a text version derived from a GVE chain. The built tree has a blank root node and a single branch stemming from it, where each node represents a character and is parent of the next character node.

- input: the execution context for GVE operations (`ChainOperationContext<char>`) and the operations themselves; or alternatively, a base text and the operations.
- output: a "multi"-tree, i.e. a blank root node having one child node per text version. Each child node is the blank root node of a linear tree and has features `sub-id`=version tag and, when present, `version`=staged version ID.

The logic of this builder is modified by its parameters:

- `FeatureNames`: the filter features names. When set, only features with names equal to the names in this list are included in the tree.
- `IsFilterInverted`: when true, the filter is inverted, i.e. only features with names NOT in the list are included in the tree.
- `TraceFeatureFlattening`: a value indicating whether to flatten trace features. When this is true, the trace features linked to nodes are not only drawn from the each processed version, but also from all the previous versions.
- `IncludeDeleted`: a value indicating whether to include deleted nodes in the tree. When this is true, for each generated version the builder will backtrack to the previous versions one after another, and insert nodes deleted by delete, replace, move, or swap operations at their place, with a delete feature. This relies on trace features: each operation injects segment-in trace features in the input version nodes, representing the nodes selected by it; and segment-out trace features in the output version nodes, representing the nodes produced or affected by it. So, when going backwards, we can find the nodes to insert by selecting nodes with segment-in features produced by operations which delete these segments, e.g. delete, replace, move, swap.
- `StagedOnly`: true to include only staged operations in the tree, i.e. the versions marked by a global `version`feature.
- `VersionTags`: the list of version tags to build trees for. If not set, all the versions will be built. Note that this filter might still be modified by `StagedOnly` when set.

The task of this builder is essentially adapting the input data to the generic rendition pipeline which follows. This is based on a linear tree, which can be variously filtered before being rendered.

Note that given the multi-dimensional nature of a GVE chain, the builder produces multiple trees, one per text version. Each of them will then have to go through the successive stages of filtering and rendering.

### 2. Text Tree Filters

Once we have a representation of the GVE text versions and their metadata into trees, for each tree we can apply filters to adapt it to the desired output format. Filters are applied in a chain, where each filter can modify the tree and pass it to the next one.

- input: a text tree.
- output: a text tree (the same instance, or a new one if modified).

Note that when starting the filtering stage we always have a linear tree; then, according to the filters used this might or not be modified into a non-linear tree with multiple branches.

A fundamental filter here is `LinearMergeTextTreeFilter`, usually applied as the first in the chain. This merges multiple nodes of a linear tree into a single node, whose text is the concatenation of the text of all the merged nodes. Merging is done by checking the features of the nodes: all the subsequent nodes having the same set of features (both for their name and their value) are merged into a single node.

Note that you are in charge of defining the features you want to be considered as merging criteria; you can define both their names, and any number of replacement filters for their values. For instance, consider this code fragment (from a unit test):

```cs
GveTextTreeBuilder builder = new()
{
    FeatureNames =
    [
        OperationFeature.F_TRACE_SEG_IN,
        OperationFeature.F_TRACE_SEG2_IN,
        OperationFeature.F_TRACE_SEG_OUT,
        OperationFeature.F_TRACE_SEG2_OUT,
        OperationFeature.F_TRACE_DELETED
    ],
    TraceFeatureFlattening = true,
    IncludeDeleted = true
};
```

Here the builder will include the trace features for segment in, segment out, and deleted nodes. Later, we configure the filter as follows:

```cs
LinearMergeTextTreeFilter filter = new();
filter.Configure(new MergeTextTreeFilterOptions
{
    Features =
    [
        OperationFeature.F_TRACE_SEG_OUT,
        OperationFeature.F_TRACE_SEG2_OUT,
        OperationFeature.F_TRACE_DELETED
    ],
    ValueFilters =
    [
        new MergeTextTreeFilterRepOption
        {
            IsRegex = true,
            Find = "^([^ ]+).*$",
            Replace = "$1",
        }
    ]
});
```

This means that we consider as grouping criteria for segmentation only the segment out and delete trace features; also, we filter their values so that we extract from them only the operation ID (the first word in the string).

So, this filter produces a dynamically generated segmentation of the text, where each segment is represented by a single node and its text is linked to the same metadata (features). Such segmentation provides maximum efficiency for the subsequent rendering, because we always use the segment with the maximum extent, whatever it is, from a single character to hundreds of them.

### 3. Render Text Tree

Once we have prepared the text tree, we can render it into the desired format. The rendering is done by an implementation of `ITextTreeRenderer`, which takes the tree and produces some representation of it.

- input: a text tree plus the renderer context. In the case of GVE, this is an instance of `GveRendererContext`, which has additional properties to include the GVE data source (chain output and its operations). This can be used by some modules in the rendering pipeline to extract more data from the GVE chain.
- output: an `object` (or null) representing the rendered text. This is not necessarily a string.

The reason for the `object` output type is flexibility. The renderer here adopts a strategy already used by Proteus in its text filters: there, each text filter internally handles its data with the model it prefers: some of them use a string; others use a `StringBuilder`; others use an `XElement`; and so forth. Their concatenation is still possible thanks to an adapter which uses different plug services. Each plug service can convert a given type from a string or into a string.

The adaptation only happens when types don't match. When two consecutive filters work on the same type (like both using XElement), no adaptation occurs. In this case, the output from the first filter passes directly to the next filter without any unnecessary string conversion. This optimization avoids the performance and potential data loss costs of converting between identical types unnecessarily.

This architecture is especially useful with modules in a pipeline, like in Proteus text filters, because each of them is free to use its best model for the task, and the pipeline can be easily extended with new modules without breaking the existing ones.

So, the same applies to the text tree renderer: its return type is an `object` (or `null`) just like for text filters, because this allows the maximum flexibility in the rendering process. The renderer can return a `string`, an `XElement`, or any other type, and the caller can then decide how to handle it.

Thus, if we are rendering a tree into XML, often the best choice is to use an `XElement` as the output type. This allows us to build a tree of XML elements, which can then be easily manipulated or serialized into a string.

Additionally, this allows to connect the result of rendering to a Proteus pipeline of text filters, which can be used to further refine the output.

So, two architectural choices are made here:

- use a **tree structure** to represent text with nodes (each having a text segment data payload), rather than just an array of text segments. This is very useful when we need to manipulate the underlying structure before materializing it into markup like XML, because we can transform the tree to branch at will.
- use a **generic object** as the output type of the renderer, which allows us to use this type-free rendition, followed by a type-free pipeline of filters for further refinement.

### 4. Filter Result

The rendering result can be further refined with the aid of a Proteus pipeline of text filters. For instance, we might have some string-based filters which make some final adjustments to the generated XML text using some replacements.

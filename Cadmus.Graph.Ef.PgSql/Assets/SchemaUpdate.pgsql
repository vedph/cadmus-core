-- Cadmus graph database: indexes and procedures.
-- This script is idempotent: it is appended to Schema.pgsql when creating
-- a new database, and applied as such to existing databases to upgrade
-- them (see PgSqlGraphSchema.ApplyAsync), so that databases created by
-- older versions get the same indexes and procedures of new ones.

-- trigram matching for "contains" searches (LIKE '%x%') on labels and URIs
CREATE EXTENSION IF NOT EXISTS pg_trgm;

-- --------------------------------------------------------
-- indexes
-- --------------------------------------------------------

-- namespace_lookup: lookups are always case-insensitive
CREATE INDEX IF NOT EXISTS namespace_lookup_id_lower_idx
	ON namespace_lookup (lower(id));
CREATE INDEX IF NOT EXISTS namespace_lookup_uri_lower_idx
	ON namespace_lookup (lower(uri));

-- uri_lookup: the hottest lookup in the whole graph (every node and triple
-- resolves its URIs here), always case-insensitive. The unique index also
-- prevents duplicate URIs. Databases created by old versions may already
-- contain duplicates differing only by case: in this case the index cannot
-- be created, and a warning is emitted (lookups then use the trigram index
-- below, and new duplicates are prevented by the repository's locks).
DO $$
BEGIN
	IF NOT EXISTS (SELECT 1 FROM pg_class WHERE relname = 'uri_lookup_uri_lower_idx') THEN
		IF EXISTS (SELECT 1 FROM uri_lookup GROUP BY lower(uri) HAVING count(*) > 1) THEN
			RAISE WARNING 'uri_lookup has URIs differing only by case: unique index uri_lookup_uri_lower_idx not created';
			CREATE INDEX IF NOT EXISTS uri_lookup_uri_lower_nu_idx ON uri_lookup (lower(uri));
		ELSE
			CREATE UNIQUE INDEX uri_lookup_uri_lower_idx ON uri_lookup (lower(uri));
		END IF;
	END IF;
END $$;
-- URI "contains" searches (node and property filters)
CREATE INDEX IF NOT EXISTS uri_lookup_uri_trgm_idx
	ON uri_lookup USING gin (lower(uri) gin_trgm_ops);

-- uid_lookup: not unique by design (several sources can share the same
-- unsuffixed UID, which is how suffixes get assigned)
CREATE INDEX IF NOT EXISTS uid_lookup_unsuffixed_lower_idx
	ON uid_lookup (lower(unsuffixed));

-- node: SID lookups (graph set of a source) are case-insensitive exact or
-- prefix matches, which the pattern operators class supports regardless
-- of the database locale
CREATE INDEX IF NOT EXISTS node_sid_lower_idx
	ON node (lower(sid) varchar_pattern_ops) WHERE sid IS NOT NULL;
-- label "contains" searches (lookup of nodes in the editor)
CREATE INDEX IF NOT EXISTS node_label_trgm_idx
	ON node USING gin (lower("label") gin_trgm_ops);
-- tag filters (e.g. properties)
CREATE INDEX IF NOT EXISTS node_tag_lower_idx
	ON node (lower(tag)) WHERE tag IS NOT NULL;

-- node_class: instance to classes, and class to instances (used when the
-- class hierarchy changes, and by the FK cascade when a class is deleted)
CREATE INDEX IF NOT EXISTS node_class_node_id_class_id_idx
	ON node_class (node_id, class_id);
CREATE INDEX IF NOT EXISTS node_class_class_id_idx
	ON node_class (class_id);

-- triple: Postgres does not index FK columns, yet these are used in
-- virtually every graph query, in node classes population, and by FK
-- cascades when deleting nodes
CREATE INDEX IF NOT EXISTS triple_sid_lower_idx
	ON triple (lower(sid) varchar_pattern_ops) WHERE sid IS NOT NULL;
CREATE INDEX IF NOT EXISTS triple_s_id_p_id_idx ON triple (s_id, p_id);
CREATE INDEX IF NOT EXISTS triple_p_id_idx ON triple (p_id);
-- replaces triple_o_id_idx, covering also linked nodes by object+predicate
CREATE INDEX IF NOT EXISTS triple_o_id_p_id_idx ON triple (o_id, p_id)
	WHERE o_id IS NOT NULL;
DROP INDEX IF EXISTS triple_o_id_idx;
-- no query can use a btree index on the filtered literal, and btree
-- entries are limited to about 2.7 KB, so that saving a long literal
-- failed with "index row size exceeds maximum"
DROP INDEX IF EXISTS triple_o_lit_ix_idx;

-- mapping
CREATE INDEX IF NOT EXISTS mapping_name_idx ON "mapping" USING btree ("name");
CREATE INDEX IF NOT EXISTS mapping_source_type_idx
	ON "mapping" USING btree (source_type);
CREATE INDEX IF NOT EXISTS mapping_link_child_id_idx ON mapping_link (child_id);
CREATE INDEX IF NOT EXISTS mapping_out_node_mapping_id_idx
	ON mapping_out_node (mapping_id);
CREATE INDEX IF NOT EXISTS mapping_out_triple_mapping_id_idx
	ON mapping_out_triple (mapping_id);
CREATE INDEX IF NOT EXISTS mapping_out_meta_mapping_id_idx
	ON mapping_out_meta (mapping_id);

-- --------------------------------------------------------
-- procedures
-- --------------------------------------------------------

-- Populates node_class for the specified node, by walking up its classes
-- (objects of triples with predicate a_id=rdf:type) and their superclasses
-- (objects of triples with predicate sub_id=rdfs:subClassOf). Each class
-- is stored once, with its minimum depth level (1=direct class). The depth
-- is limited, so that cycles in the classes hierarchy cannot make the
-- recursion endless.
CREATE OR REPLACE PROCEDURE populate_node_class(instance_id integer,
	a_id integer, sub_id integer)
 LANGUAGE sql
AS $function$
	DELETE FROM node_class WHERE node_id = instance_id;

	INSERT INTO node_class(node_id, class_id, "level")
	WITH RECURSIVE cn(id, "level") AS (
		-- anchor: direct classes
		SELECT t.o_id, 1 FROM triple t
		WHERE t.s_id = instance_id AND t.p_id = a_id AND t.o_id IS NOT NULL
		UNION
		-- recursion: superclasses
		SELECT t.o_id, cn."level" + 1 FROM cn
		INNER JOIN triple t ON t.s_id = cn.id AND t.p_id = sub_id
		WHERE t.o_id IS NOT NULL AND cn."level" < 32
	)
	SELECT instance_id, cn.id, min(cn."level") FROM cn GROUP BY cn.id;
$function$;

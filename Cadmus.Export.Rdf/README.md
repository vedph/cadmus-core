# Cadmus.Export.Rdf

This library contains components used to export a Cadmus semantic graph into various RDF formats.

The main component here is `RdfExporter`, which gets constructed with a connection string to the source PostgreSQL database and export settings, and can be used to export triples into an output file.

According to its settings, this exporter creates the proper RDF writer, which can be any of:

- `JsonLdWriter`
- `NTriplesRdfWriter`
- `TurtleRdfWriter`
- `XmlRdfWriter`

`RamRdfWriter` exports in-memory data, and is used for testing.

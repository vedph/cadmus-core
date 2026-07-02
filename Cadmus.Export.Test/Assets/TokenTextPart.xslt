<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" xmlns:tei="http://www.tei-c.org/ns/1.0" version="1.0">
	<xsl:output method="text" encoding="UTF-8"/>
	<xsl:strip-space elements="*" />

	<xsl:template match="citation">[<xsl:value-of select="."/>]<xsl:text xml:space="preserve">&#xA;</xsl:text>
</xsl:template>

	<xsl:template match="lines">
		<xsl:apply-templates/>
	</xsl:template>

	<xsl:template match="line">
		<xsl:value-of select="y"/>
		<xsl:text xml:space="preserve">  </xsl:text>
		<xsl:value-of select="text"/>
		<xsl:text xml:space="preserve">&#xA;</xsl:text>
	</xsl:template>

	<xsl:template match="root">
		<xsl:apply-templates/>
	</xsl:template>

	<xsl:template match="*"></xsl:template>
</xsl:stylesheet>
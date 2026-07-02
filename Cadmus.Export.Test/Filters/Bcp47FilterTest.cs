using Cadmus.Export.Filters;
using System.Collections.Generic;
using Xunit;

namespace Cadmus.Export.Test.Filters;

public sealed class Bcp47FilterTest
{
    [Fact]
    public void Apply_NoMatch_Unchanged()
    {
        Bcp47TextFilter filter = new();
        const string text = "Hello, world!";

        string? filtered = filter.Apply(text)?.ToString();

        Assert.Equal(text, filtered);
    }

    [Fact]
    public void Apply_MatchInvalidCode_Code()
    {
        Bcp47TextFilter filter = new();
        const string text = "Hello, ^^xyz world!";

        string? filtered = filter.Apply(text)?.ToString();

        Assert.Equal("Hello, xyz world!", filtered);
    }

    [Fact]
    public void Apply_TwoLetterCode_Changed()
    {
        Bcp47TextFilter filter = new();
        const string text = "Hello, ^^en and ^^it world!";

        string? filtered = filter.Apply(text)?.ToString();

        Assert.Equal("Hello, English and Italian world!", filtered);
    }

    [Fact]
    public void Apply_ThreeLetterCode_Changed()
    {
        Bcp47TextFilter filter = new();
        const string text = "Text in ^^grc and ^^akk languages.";

        string? filtered = filter.Apply(text)?.ToString();

        Assert.Equal("Text in Ancient Greek and Akkadian languages.",
            filtered);
    }

    [Fact]
    public void Apply_AncientLanguages_Changed()
    {
        Bcp47TextFilter filter = new();
        const string text = "Ancient texts: ^^akk, ^^egy, ^^sux, ^^grc.";

        string? filtered = filter.Apply(text)?.ToString();

        Assert.Equal(
            "Ancient texts: Akkadian, Ancient Egyptian, Sumerian, " +
            "Ancient Greek.", filtered);
    }

    [Fact]
    public void Apply_HistoricalLanguages_Changed()
    {
        Bcp47TextFilter filter = new();
        const string text = "Medieval: ^^ang, ^^enm, ^^fro, ^^gmh.";

        string? filtered = filter.Apply(text)?.ToString();

        Assert.Equal(
            "Medieval: Old English, Middle English, Old French, " +
            "Middle High German.", filtered);
    }

    [Fact]
    public void Apply_MixedModernAndAncient_Changed()
    {
        Bcp47TextFilter filter = new();
        const string text = "Compare ^^el with ^^grc.";

        string? filtered = filter.Apply(text)?.ToString();

        Assert.Equal("Compare Modern Greek with Ancient Greek.",
            filtered);
    }

    [Fact]
    public void Apply_MultipleCodes_Changed()
    {
        Bcp47TextFilter filter = new();
        const string text =
            "Languages: ^^en, ^^de, ^^fr, ^^es, ^^it, ^^pt.";

        string? filtered = filter.Apply(text)?.ToString();

        Assert.Equal(
            "Languages: English, German, French, Spanish, Italian, " +
            "Portuguese.", filtered);
    }

    [Fact]
    public void Apply_CustomTag_Changed()
    {
        Bcp47TextFilter filter = new();
        filter.Configure(new Bcp47FilterOptions
        {
            CustomTagNames = new Dictionary<string, string>
            {
                { "myc", "Mycenaean Greek" },
                { "dor", "Doric Greek" }
            }
        });
        const string text = "Ancient dialects: ^^myc and ^^dor.";

        string? filtered = filter.Apply(text)?.ToString();

        Assert.Equal("Ancient dialects: Mycenaean Greek and Doric Greek.",
            filtered);
    }

    [Fact]
    public void Apply_CustomTagWithFallback_Changed()
    {
        Bcp47TextFilter filter = new();
        filter.Configure(new Bcp47FilterOptions
        {
            CustomTagNames = new Dictionary<string, string>
            {
                { "myc", "Mycenaean Greek" }
            }
        });
        // grc is in standard list, xyz is not
        const string text = "^^myc, ^^grc, and ^^xyz.";

        string? filtered = filter.Apply(text)?.ToString();

        Assert.Equal("Mycenaean Greek, Ancient Greek, and xyz.",
            filtered);
    }

    [Fact]
    public void Apply_CustomPattern_Changed()
    {
        Bcp47TextFilter filter = new();
        filter.Configure(new Bcp47FilterOptions
        {
            Pattern = @"\[lang:([a-z]{2,3})\]"
        });
        const string text = "Text in [lang:en] and [lang:grc].";

        string? filtered = filter.Apply(text)?.ToString();

        Assert.Equal("Text in English and Ancient Greek.", filtered);
    }

    [Fact]
    public void Apply_CaseInsensitive_Changed()
    {
        Bcp47TextFilter filter = new();
        filter.Configure(new Bcp47FilterOptions
        {
            Pattern = @"\^\^([a-zA-Z]{2,3})",
            CustomTagNames = new Dictionary<string, string>
            {
                { "myc", "Mycenaean Greek" }
            }
        });
        const string text = "^^EN and ^^MYC.";

        string? filtered = filter.Apply(text)?.ToString();

        Assert.Equal("English and Mycenaean Greek.", filtered);
    }

    [Fact]
    public void Apply_SemiticLanguages_Changed()
    {
        Bcp47TextFilter filter = new();
        const string text = "Semitic: ^^ar, ^^he, ^^arc, ^^akk.";

        string? filtered = filter.Apply(text)?.ToString();

        Assert.Equal("Semitic: Arabic, Hebrew, Aramaic, Akkadian.",
            filtered);
    }

    [Fact]
    public void Apply_AsianLanguages_Changed()
    {
        Bcp47TextFilter filter = new();
        const string text = "Asian: ^^zh, ^^ja, ^^ko, ^^sa, ^^pi.";

        string? filtered = filter.Apply(text)?.ToString();

        Assert.Equal("Asian: Chinese, Japanese, Korean, Sanskrit, Pali.",
            filtered);
    }

    [Fact]
    public void Apply_NullText_Null()
    {
        Bcp47TextFilter filter = new();

        string? filtered = filter.Apply(null)?.ToString();

        Assert.Null(filtered);
    }

    [Fact]
    public void Apply_EmptyText_Empty()
    {
        Bcp47TextFilter filter = new();
        const string text = "";

        string? filtered = filter.Apply(text)?.ToString();

        Assert.Equal(text, filtered);
    }

    [Fact]
    public void Apply_GothicAndOtherExtinct_Changed()
    {
        Bcp47TextFilter filter = new();
        const string text = "Extinct: ^^got, ^^hit, ^^cop, ^^sux.";

        string? filtered = filter.Apply(text)?.ToString();

        Assert.Equal(
            "Extinct: Gothic, Hittite, Coptic, Sumerian.", filtered);
    }

    [Fact]
    public void Apply_RegionSubtag_Changed()
    {
        Bcp47TextFilter filter = new();
        const string text = "Text in ^^en-US and ^^en-GB.";

        string? filtered = filter.Apply(text)?.ToString();

        Assert.Equal(
            "Text in English (United States) and " +
            "English (United Kingdom).", filtered);
    }

    [Fact]
    public void Apply_MultipleRegionSubtags_Changed()
    {
        Bcp47TextFilter filter = new();
        const string text = "Languages: ^^es-ES, ^^es-MX, ^^es-AR.";

        string? filtered = filter.Apply(text)?.ToString();

        Assert.Equal(
            "Languages: Spanish (Spain), Spanish (Mexico), " +
            "Spanish (Argentina).", filtered);
    }

    [Fact]
    public void Apply_ChineseVariants_Changed()
    {
        Bcp47TextFilter filter = new();
        const string text = "Chinese: ^^zh-CN, ^^zh-TW, ^^zh-HK.";

        string? filtered = filter.Apply(text)?.ToString();

        Assert.Equal(
            "Chinese: Chinese (Simplified), Chinese (Traditional), " +
            "Chinese (Hong Kong).", filtered);
    }

    [Fact]
    public void Apply_PortugueseVariants_Changed()
    {
        Bcp47TextFilter filter = new();
        const string text = "Portuguese: ^^pt-PT vs ^^pt-BR.";

        string? filtered = filter.Apply(text)?.ToString();

        Assert.Equal(
            "Portuguese: Portuguese (Portugal) vs " +
            "Portuguese (Brazil).", filtered);
    }

    [Fact]
    public void Apply_FrenchVariants_Changed()
    {
        Bcp47TextFilter filter = new();
        const string text = "French: ^^fr-FR, ^^fr-CA, ^^fr-BE, ^^fr-CH.";

        string? filtered = filter.Apply(text)?.ToString();

        Assert.Equal(
            "French: French (France), French (Canada), French (Belgium), " +
            "French (Switzerland).", filtered);
    }

    [Fact]
    public void Apply_CustomSubtag_Changed()
    {
        Bcp47TextFilter filter = new();
        filter.Configure(new Bcp47FilterOptions
        {
            CustomTagNames = new Dictionary<string, string>
            {
                { "en-US-medical", "American English (Medical)" },
                { "en-US-legal", "American English (Legal)" }
            }
        });
        const string text = "Texts: ^^en-US-medical and ^^en-US-legal.";

        string? filtered = filter.Apply(text)?.ToString();

        Assert.Equal(
            "Texts: American English (Medical) and " +
            "American English (Legal).", filtered);
    }

    [Fact]
    public void Apply_CustomSubtagWithStandardFallback_Changed()
    {
        Bcp47TextFilter filter = new();
        filter.Configure(new Bcp47FilterOptions
        {
            CustomTagNames = new Dictionary<string, string>
            {
                { "en-US-medical", "American English (Medical)" }
            }
        });
        // en-US is standard, en-US-medical is custom,
        // en-US-unknown is not mapped
        const string text =
            "Tags: ^^en-US, ^^en-US-medical, ^^en-US-unknown.";

        string? filtered = filter.Apply(text)?.ToString();

        Assert.Equal(
            "Tags: English (United States), " +
            "American English (Medical), en-US-unknown.", filtered);
    }

    [Fact]
    public void Apply_MixedBasicAndRegionTags_Changed()
    {
        Bcp47TextFilter filter = new();
        const string text = "Mix: ^^en, ^^en-US, ^^fr, ^^fr-CA.";

        string? filtered = filter.Apply(text)?.ToString();

        Assert.Equal(
            "Mix: English, English (United States), French, " +
            "French (Canada).", filtered);
    }

    [Fact]
    public void Apply_GermanVariants_Changed()
    {
        Bcp47TextFilter filter = new();
        const string text = "German: ^^de-DE, ^^de-AT, ^^de-CH.";

        string? filtered = filter.Apply(text)?.ToString();

        Assert.Equal(
            "German: German (Germany), German (Austria), " +
            "German (Switzerland).", filtered);
    }

    [Fact]
    public void Apply_ExtendedCustomSubtags_Changed()
    {
        Bcp47TextFilter filter = new();
        filter.Configure(new Bcp47FilterOptions
        {
            CustomTagNames = new Dictionary<string, string>
            {
                { "grc-koine", "Koine Greek" },
                { "grc-attic", "Attic Greek" },
                { "la-medieval", "Medieval Latin" },
                { "la-ecclesiastical", "Ecclesiastical Latin" }
            }
        });
        const string text =
            "Ancient: ^^grc-koine, ^^grc-attic, ^^la-medieval.";

        string? filtered = filter.Apply(text)?.ToString();

        Assert.Equal(
            "Ancient: Koine Greek, Attic Greek, Medieval Latin.",
            filtered);
    }

    [Fact]
    public void Apply_CaseInsensitiveSubtags_Changed()
    {
        Bcp47TextFilter filter = new();
        const string text = "Tags: ^^EN-us, ^^Fr-CA, ^^DE-ch.";

        string? filtered = filter.Apply(text)?.ToString();

        Assert.Equal(
            "Tags: English (United States), French (Canada), " +
            "German (Switzerland).", filtered);
    }

    [Fact]
    public void Apply_UnknownRegionSubtag_Fallback()
    {
        Bcp47TextFilter filter = new();
        const string text = "Unknown: ^^en-XX.";

        string? filtered = filter.Apply(text)?.ToString();

        Assert.Equal("Unknown: en-XX.", filtered);
    }
}

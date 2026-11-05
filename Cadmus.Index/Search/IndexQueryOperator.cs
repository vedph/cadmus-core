namespace Cadmus.Index.Search;

/// <summary>
/// The operator of a clause in an index query.
/// </summary>
public enum IndexQueryOperator
{
    /// <summary>Equal (<c>=</c>).</summary>
    Equal = 0,
    /// <summary>Not equal (<c>&lt;&gt;</c>).</summary>
    NotEqual,
    /// <summary>Contains (<c>*=</c>).</summary>
    Contains,
    /// <summary>Starts with (<c>^=</c>).</summary>
    StartsWith,
    /// <summary>Ends with (<c>$=</c>).</summary>
    EndsWith,
    /// <summary>Wildcards (<c>?=</c>): <c>?</c>=any single character,
    /// <c>*</c>=0 or more characters.</summary>
    Wildcards,
    /// <summary>Regular expression (<c>~=</c>).</summary>
    Regex,
    /// <summary>Fuzzy similarity (<c>%=</c>), with an optional threshold
    /// appended to the value after a colon.</summary>
    Similar,
    /// <summary>Numeric (or date) equal (<c>==</c>).</summary>
    NumEqual,
    /// <summary>Numeric (or date) not equal (<c>!=</c>).</summary>
    NumNotEqual,
    /// <summary>Numeric (or date) less than (<c>&lt;</c>).</summary>
    NumLess,
    /// <summary>Numeric (or date) less than or equal (<c>&lt;=</c>).
    /// </summary>
    NumLessOrEqual,
    /// <summary>Numeric (or date) greater than (<c>&gt;</c>).</summary>
    NumGreater,
    /// <summary>Numeric (or date) greater than or equal (<c>&gt;=</c>).
    /// </summary>
    NumGreaterOrEqual,
    /// <summary>Has any of the specified flags (<c>:</c>).</summary>
    HasAnyFlags,
    /// <summary>Has all the specified flags (<c>&amp;:</c>).</summary>
    HasAllFlags,
    /// <summary>Has none of the specified flags (<c>!:</c>).</summary>
    HasNoFlags,
}

using Microsoft.AspNetCore.Components;
using System.ComponentModel.DataAnnotations;

namespace CadmusPreviewBuilder.Pages;

public class BuilderModel
{
    public bool IsFragment { get; set; }
    public bool IsWrapEnabled { get; set; }

    [Required]
    public string Json { get; set; }

    [Required]
    public string Config { get; set; }

    public string? Xml { get; set; }

    public string? Css { get; set; }

    public bool IsRunning { get; set; }

    public string? Html { get; set; }

    public MarkupString Result { get; set; }

    public string? Error { get; set; }

    public BuilderModel()
    {
        Json = "{}";
        Config = "{}";
    }
}

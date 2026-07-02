using System.ComponentModel.DataAnnotations;

namespace Cadmus.GraphStudio.Api.Models;

public class JmesTransformBindingModel
{
    /// <summary>
    /// The input JSON.
    /// </summary>
    [Required]
    [MaxLength(50000)]
    public string Json { get; set; }

    /// <summary>
    /// Gets or sets the JMES expression.
    /// </summary>
    [Required]
    [MaxLength(5000)]
    public string Expression { get; set; }

    public JmesTransformBindingModel()
    {
        Json = "";
        Expression = "";
    }
}

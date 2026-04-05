using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace StripUserIntegration.Models;

public partial class TblUser
{
    [Required]
    public int Uuid { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public int Amount { get; set; }

    [Required]
    public string Status { get; set; } = string.Empty;

    public DateTime CreatedDate { get; set; }

    public DateTime UpdatedDate { get; set; }

    //[JsonPropertyName("metadata")]
    //public Metadata Metadata { get; set; }
}
/*public class Metadata
{
    [JsonPropertyName("internalUserId")]
    public string InternalUserId { get; set; }
}*/

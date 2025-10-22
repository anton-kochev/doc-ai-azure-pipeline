using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DocProcessing.Domain.Entities;

/// <summary>
/// Represents a versioned extraction profile configuration in the catalog.
/// Profiles define how documents should be processed and what data should be extracted.
/// </summary>
[Index(nameof(Status), Name = "IX_ProfileCatalog_Status")]
[PrimaryKey(nameof(ProfileName), nameof(Version))]
public sealed class ProfileCatalog
{
    /// <summary>
    /// Gets or sets the unique name of the extraction profile.
    /// </summary>
    [Required]
    [MaxLength(128)]
    [Column(TypeName = "nvarchar(128)")]
    public string ProfileName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the version of the profile (e.g., "1.0.0", "2.1.0").
    /// </summary>
    [Required]
    [MaxLength(32)]
    [Column(TypeName = "varchar(32)")]
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current status of the profile.
    /// </summary>
    [Required]
    [MaxLength(16)]
    [Column(TypeName = "varchar(16)")]
    public ProfileStatus Status { get; set; } = ProfileStatus.Draft;

    /// <summary>
    /// Gets or sets whether this is the default profile to use when none is specified.
    /// </summary>
    [Required]
    [Column(TypeName = "bit")]
    public bool IsDefault { get; set; } = false;

    /// <summary>
    /// Gets or sets the profile configuration in JSON format.
    /// </summary>
    [Required]
    [Column(TypeName = "nvarchar(max)")]
    public string ConfigJson { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets an optional description of the profile and its purpose.
    /// </summary>
    [MaxLength(512)]
    [Column(TypeName = "nvarchar(512)")]
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user who created the profile.
    /// </summary>
    [Required]
    [MaxLength(256)]
    [Column(TypeName = "nvarchar(256)")]
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the UTC timestamp when the profile was created.
    /// </summary>
    [Required]
    [Column(TypeName = "datetime2")]
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user who last updated the profile.
    /// </summary>
    [MaxLength(256)]
    [Column(TypeName = "nvarchar(256)")]
    public string? UpdatedBy { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the profile was last updated.
    /// </summary>
    [Column(TypeName = "datetime2")]
    public DateTime? UpdatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the SHA256 checksum of the configuration for integrity verification.
    /// </summary>
    [MaxLength(64)]
    [Column(TypeName = "char(64)")]
    public string? Checksum { get; set; }

    /// <summary>
    /// Gets or sets the row version for optimistic concurrency control.
    /// </summary>
    [Timestamp]
    public byte[]? RowVersion { get; set; }
}

/// <summary>
/// Defines the lifecycle status of an extraction profile.
/// </summary>
public enum ProfileStatus
{
    /// <summary>
    /// The profile is being drafted and is not yet ready for use.
    /// </summary>
    Draft,

    /// <summary>
    /// The profile is active and available for use in processing.
    /// </summary>
    Active,

    /// <summary>
    /// The profile is deprecated but still available for backward compatibility.
    /// </summary>
    Deprecated,

    /// <summary>
    /// The profile has been retired and should no longer be used.
    /// </summary>
    Retired
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace api.Data.Entities;

[Index(nameof(Status), Name = "IX_ProfileCatalog_Status")]
[PrimaryKey(nameof(ProfileName), nameof(Version))]
public sealed class ProfileCatalog
{
    [Required]
    [MaxLength(128)]
    [Column(TypeName = "nvarchar(128)")]
    public string ProfileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(32)]
    [Column(TypeName = "varchar(32)")]
    public string Version { get; set; } = string.Empty;

    [Required]
    [MaxLength(16)]
    [Column(TypeName = "varchar(16)")]
    public ProfileStatus Status { get; set; } = ProfileStatus.Draft;

    [Required]
    [Column(TypeName = "bit")]
    public bool IsDefault { get; set; } = false;

    [Required]
    [Column(TypeName = "nvarchar(max)")]
    public string ConfigJson { get; set; } = string.Empty;

    [MaxLength(512)]
    [Column(TypeName = "nvarchar(512)")]
    public string? Description { get; set; }

    [Required]
    [MaxLength(256)]
    [Column(TypeName = "nvarchar(256)")]
    public string CreatedBy { get; set; } = string.Empty;

    [Required]
    [Column(TypeName = "datetime2")]
    public DateTime CreatedAtUtc { get; set; }

    [MaxLength(256)]
    [Column(TypeName = "nvarchar(256)")]
    public string? UpdatedBy { get; set; }

    [Column(TypeName = "datetime2")]
    public DateTime? UpdatedAtUtc { get; set; }

    [MaxLength(64)]
    [Column(TypeName = "char(64)")]
    public string? Checksum { get; set; }

    [Timestamp]
    public byte[]? RowVersion { get; set; }
}

public enum ProfileStatus
{
    Draft,
    Active,
    Deprecated,
    Retired
}

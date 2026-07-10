namespace AuditX.Application.Abstractions;

/// <summary>Generates opaque, URL-safe, high-entropy tokens for shareable-link slugs (D3-B).</summary>
public interface ISlugGenerator
{
    /// <summary>A new URL-safe slug with at least 128 bits of entropy.</summary>
    string NewSlug();
}

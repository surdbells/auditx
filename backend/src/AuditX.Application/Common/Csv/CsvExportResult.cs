namespace AuditX.Application.Common.Csv;

/// <summary>A rendered CSV export: the bytes, their SHA-256 (integrity anchor), and download metadata.</summary>
public sealed record CsvExportResult(string FileName, string ContentType, string Sha256, int RowCount, byte[] Content);

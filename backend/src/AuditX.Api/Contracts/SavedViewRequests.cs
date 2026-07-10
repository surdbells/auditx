namespace AuditX.Api.Contracts;

/// <summary>Create a saved view for a screen. <c>ParametersJson</c> is the opaque filter payload the frontend applies.</summary>
public sealed record CreateSavedViewRequest(string ViewKey, string Name, string ParametersJson, bool IsShared);

/// <summary>Update an owned saved view (name / parameters / shared flag) with its rowversion.</summary>
public sealed record UpdateSavedViewRequest(string Name, string ParametersJson, bool IsShared, string Version);

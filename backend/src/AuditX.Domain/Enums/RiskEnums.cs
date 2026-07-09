namespace AuditX.Domain.Enums;

/// <summary>Lifecycle of an enterprise risk-register entry (P1-A).</summary>
public enum RiskStatus
{
    Open,
    Assessed,
    Mitigating,
    Monitoring,
    Closed,
}

/// <summary>The ISO 31000 treatment strategy chosen for a risk.</summary>
public enum RiskTreatmentStrategy
{
    Accept,
    Mitigate,
    Transfer,
    Avoid,
}

/// <summary>Severity band derived from a likelihood×impact score (1–25).</summary>
public enum RiskBand
{
    Low,
    Medium,
    High,
    Critical,
}

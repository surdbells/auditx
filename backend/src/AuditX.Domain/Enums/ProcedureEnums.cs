namespace AuditX.Domain.Enums;

/// <summary>The kind of fieldwork procedure an auditor performed (P2-C).</summary>
public enum ProcedureType
{
    Sampling,
    Interview,
    Walkthrough,
}

/// <summary>How a sample was selected (P2-C); only meaningful for <see cref="ProcedureType.Sampling"/>.</summary>
public enum SamplingMethod
{
    Random,
    Systematic,
    Judgmental,
    Haphazard,
}

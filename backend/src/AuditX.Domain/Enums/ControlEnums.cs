namespace AuditX.Domain.Enums;

/// <summary>The nature of an internal control (P1-B).</summary>
public enum ControlType
{
    Preventive,
    Detective,
    Corrective,
    Directive,
}

/// <summary>How often a control operates / is performed.</summary>
public enum ControlFrequency
{
    Continuous,
    Daily,
    Weekly,
    Monthly,
    Quarterly,
    SemiAnnual,
    Annual,
    AdHoc,
}

/// <summary>The tested operating effectiveness of a control.</summary>
public enum ControlEffectiveness
{
    NotTested,
    Effective,
    PartiallyEffective,
    Ineffective,
}

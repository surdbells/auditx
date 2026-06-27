namespace AuditX.Domain.Enums;

/// <summary>The render type of a dashboard widget (M9). Persisted snake_case (chart, table, single_metric).</summary>
public enum WidgetType
{
    Chart,
    Table,
    SingleMetric,
}

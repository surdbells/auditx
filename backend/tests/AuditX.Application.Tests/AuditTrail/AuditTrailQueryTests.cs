using System.Text;
using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Persistence;
using AuditX.Application.AuditTrail.Queries;
using AuditX.Application.Common.Models;
using AuditX.Domain.AuditTrail;
using NSubstitute;

namespace AuditX.Application.Tests.AuditTrail;

public sealed class AuditTrailQueryTests
{
    private static AuditTrailEntryView View(string eventType = "audit_created") => new(
        Guid.CreateVersion7(), eventType, "audit", Guid.NewGuid(), Guid.NewGuid(), "User", null,
        DateTimeOffset.UnixEpoch, "Africa/Lagos", "{\"a\":1}", "{\"a\":2}", "{\"ip\":\"10.0.0.1\"}", null);

    [Fact]
    public async Task QueryAuditTrail_passes_filters_to_the_reader_and_self_audits()
    {
        var reader = Substitute.For<IAuditTrailReader>();
        var audit = Substitute.For<IAuditRecorder>();
        var uow = Substitute.For<IUnitOfWork>();
        var actor = Guid.NewGuid();
        reader.QueryAsync(Arg.Any<AuditTrailFilter>(), Arg.Any<PageRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CursorPage<AuditTrailEntryView>([View()], "next", true));

        var handler = new QueryAuditTrailQueryHandler(reader, audit, uow);
        var result = await handler.Handle(
            new QueryAuditTrailQuery(actor, "audit_created", "audit", null, null, null, null, 50), CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal("next", result.NextCursor);
        await reader.Received(1).QueryAsync(
            Arg.Is<AuditTrailFilter>(f => f.ActorUserId == actor && f.EventType == "audit_created" && f.TargetObjectType == "audit"),
            Arg.Any<PageRequest>(), Arg.Any<CancellationToken>());
        audit.Received(1).Record(AuditEventTypes.TrailQueried, AuditTargetTypes.AuditTrail, null,
            Arg.Any<object?>(), Arg.Any<object?>(), Arg.Any<object?>());
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExportAuditTrail_writes_csv_with_a_header_hash_and_records_the_export()
    {
        var reader = Substitute.For<IAuditTrailReader>();
        var clock = Substitute.For<IClock>();
        var audit = Substitute.For<IAuditRecorder>();
        var uow = Substitute.For<IUnitOfWork>();
        clock.UtcNow.Returns(DateTimeOffset.UnixEpoch);
        reader.StreamAsync(Arg.Any<AuditTrailFilter>(), Arg.Any<CancellationToken>()).Returns(Stream(View(), View()));

        var handler = new ExportAuditTrailQueryHandler(reader, clock, audit, uow);
        var export = await handler.Handle(new ExportAuditTrailQuery(null, null, null, null, null, null), CancellationToken.None);

        Assert.Equal("text/csv", export.ContentType);
        Assert.Equal(2, export.RowCount);
        Assert.Equal(64, export.Sha256.Length); // SHA-256 hex
        var csv = Encoding.UTF8.GetString(export.Content);
        Assert.StartsWith("id,occurred_at_utc,actor_type,actor_user_id,event_type,target_object_type,target_object_id", csv);
        Assert.DoesNotContain("\"a\":1", csv); // before/after JSON blobs are deliberately excluded from the export
        audit.Received(1).Record(AuditEventTypes.TrailExported, AuditTargetTypes.AuditTrail, null,
            Arg.Any<object?>(), Arg.Any<object?>(), Arg.Any<object?>());
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private static async IAsyncEnumerable<AuditTrailEntryView> Stream(params AuditTrailEntryView[] views)
    {
        foreach (var v in views)
        {
            yield return v;
        }

        await Task.CompletedTask;
    }
}

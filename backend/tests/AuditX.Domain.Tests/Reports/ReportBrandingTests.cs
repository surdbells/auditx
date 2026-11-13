using AuditX.Domain.Common;
using AuditX.Domain.Reports;

namespace AuditX.Domain.Tests.Reports;

public sealed class ReportBrandingTests
{
    private const string Png1X1 = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;

    private static Report NewReport() => Report.Start(
        Guid.NewGuid(), versionNumber: 1, Guid.NewGuid(), templateVersion: 1, "{\"sections\":[]}",
        ["html"], Guid.NewGuid(), Now);

    [Fact]
    public void Create_keeps_a_valid_branding_as_given()
    {
        var b = ReportBranding.Create("  Alpha Bank  ", "#0A5C36", "#C9A227", Png1X1);

        Assert.Equal("Alpha Bank", b.InstitutionName);
        Assert.Equal("#0a5c36", b.PrimaryColor); // normalised to lower-case
        Assert.Equal("#c9a227", b.AccentColor);
        Assert.Equal(Png1X1, b.LogoDataUri);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("red")]
    [InlineData("#fff")]
    [InlineData("#12345g")]
    [InlineData("#123456;background:url(https://evil.example/x)")] // must never reach a stylesheet
    public void A_malformed_colour_falls_back_to_the_default(string? colour)
    {
        var b = ReportBranding.Create("Alpha Bank", colour, colour, null);

        Assert.Equal(ReportBranding.DefaultPrimaryColor, b.PrimaryColor);
        Assert.Equal(ReportBranding.DefaultAccentColor, b.AccentColor);
    }

    [Theory]
    [InlineData("https://evil.example/logo.png")] // a remote URL would be fetched when the report is opened
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html;base64,PHNjcmlwdD5hbGVydCgxKTwvc2NyaXB0Pg==")]
    [InlineData("data:image/png;base64,not base64 !!")]
    [InlineData("data:image/png,rawdata")] // not base64-encoded
    [InlineData("data:image/png;base64,AAAA\" onerror=\"alert(1)")] // attribute break-out attempt
    public void An_unsafe_or_malformed_logo_is_dropped(string logo)
    {
        Assert.Null(ReportBranding.Create("Alpha Bank", "#0a5c36", "#c9a227", logo).LogoDataUri);
    }

    [Fact]
    public void A_blank_name_falls_back_and_an_overlong_one_is_capped()
    {
        Assert.Equal(ReportBranding.DefaultInstitutionName, ReportBranding.Create("   ", null, null, null).InstitutionName);
        Assert.Equal(200, ReportBranding.Create(new string('x', 500), null, null, null).InstitutionName.Length);
    }

    [Fact]
    public void DecodeLogo_returns_the_media_type_and_bytes()
    {
        var logo = ReportBranding.Create("Alpha Bank", null, null, Png1X1).DecodeLogo();

        Assert.NotNull(logo);
        Assert.Equal("image/png", logo.Value.MediaType);
        Assert.Equal(0x89, logo.Value.Bytes[0]); // PNG signature
        Assert.Null(ReportBranding.Default.DecodeLogo());
    }

    [Fact]
    public void The_snapshot_records_the_logo_by_hash_not_by_content()
    {
        var json = ReportBranding.Create("Alpha Bank", "#0a5c36", "#c9a227", Png1X1).ToSnapshotJson();
        var snapshot = ReportBranding.ReadSnapshot(json);

        Assert.DoesNotContain("base64", json);
        Assert.True(json.Length < 400); // stays small on the report row whatever the logo size
        Assert.NotNull(snapshot);
        Assert.Equal("Alpha Bank", snapshot.InstitutionName);
        Assert.Equal("#0a5c36", snapshot.PrimaryColor);
        Assert.Equal(64, snapshot.LogoSha256!.Length);
    }

    [Fact]
    public void A_logo_free_branding_snapshots_with_no_logo_hash()
    {
        var snapshot = ReportBranding.ReadSnapshot(ReportBranding.Default.ToSnapshotJson());

        Assert.NotNull(snapshot);
        Assert.Null(snapshot.LogoSha256);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    public void An_absent_or_unreadable_snapshot_reads_as_null(string? json)
    {
        Assert.Null(ReportBranding.ReadSnapshot(json));
    }

    // ---- The report freezes its branding ---------------------------------------------------

    [Fact]
    public void A_report_captures_its_branding_while_being_generated()
    {
        var report = NewReport();
        Assert.Null(report.BrandingSnapshotJson);

        report.MarkRunning();
        report.SnapshotBranding(ReportBranding.Create("Alpha Bank", "#0a5c36", "#c9a227", null));

        Assert.Equal("Alpha Bank", ReportBranding.ReadSnapshot(report.BrandingSnapshotJson)!.InstitutionName);
    }

    [Fact]
    public void Branding_is_captured_once_and_cannot_be_replaced()
    {
        var report = NewReport();
        report.MarkRunning();
        report.SnapshotBranding(ReportBranding.Create("Alpha Bank", "#0a5c36", "#c9a227", null));

        var ex = Assert.Throws<InvalidStateTransitionException>(
            () => report.SnapshotBranding(ReportBranding.Create("Beta Bank", "#b00020", "#333333", null)));

        Assert.Equal("report.branding_already_set", ex.Code);
        Assert.Equal("Alpha Bank", ReportBranding.ReadSnapshot(report.BrandingSnapshotJson)!.InstitutionName);
    }

    [Fact]
    public void An_issued_report_can_never_be_rebranded()
    {
        // Even a report issued before snapshots existed (no branding recorded) is closed to branding once completed.
        var report = NewReport();
        report.MarkRunning();
        report.Complete("hash-1", [new ProducedArtefact("html", "reports/x/v1/html/a.html", "text/html", 123, "abc123")], "[{}]", Now);

        var ex = Assert.Throws<InvalidStateTransitionException>(
            () => report.SnapshotBranding(ReportBranding.Create("Beta Bank", "#b00020", "#333333", null)));

        Assert.Equal("report.branding_locked", ex.Code);
        Assert.Null(report.BrandingSnapshotJson);
    }
}

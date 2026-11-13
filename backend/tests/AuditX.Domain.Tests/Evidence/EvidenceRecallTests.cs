using AuditX.Domain.Common;
using AuditX.Domain.Enums;
using AuditX.Domain.Evidence;
using AuditX.Domain.Evidence.Events;

namespace AuditX.Domain.Tests.Evidence;

/// <summary>
/// A document provided for an evidence request can be recalled by the person who provided it — and recalling never
/// erases anything: the document's record is kept, marked recalled with who / when / why.
/// </summary>
public sealed class EvidenceRecallTests
{
    private static readonly Guid AuditId = Guid.NewGuid();
    private static readonly Guid Auditor = Guid.NewGuid();
    private static readonly Guid Auditee = Guid.NewGuid();
    private static readonly DateTimeOffset UploadedAt = new(2027, 1, 12, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = new(2027, 1, 13, 14, 30, 0, TimeSpan.Zero);

    private static EvidenceFile NewFile(EvidenceContextType context = EvidenceContextType.EvidenceRequest)
        => EvidenceFile.Create(AuditId, context, Guid.NewGuid(), "audits/x/evidence-requests/y/z", "december.pdf",
            "application/pdf", 2048, new string('a', 64), Auditee, UploadedAt);

    private static EvidenceRequest NewRequest()
        => EvidenceRequest.Request(AuditId, "Branch Audit", null, null, EvidenceRequestPurpose.ReviewDocument,
            "December reconciliation", "reconciliation", Auditor, Auditee, new DateOnly(2027, 1, 10), null, null);

    // ---- The document ---------------------------------------------------------------------------

    [Fact]
    public void Recalling_marks_the_document_with_who_when_and_why_and_keeps_everything_else()
    {
        var file = NewFile();

        file.Recall("  Uploaded the wrong month.  ", Auditee, Now);

        Assert.True(file.IsRecalled);
        Assert.Equal(Auditee, file.RecalledBy);
        Assert.Equal(Now, file.RecalledAt);
        Assert.Equal("Uploaded the wrong month.", file.RecallReason);
        // History remains: the record is not deleted and what was uploaded is untouched.
        Assert.False(file.IsDeleted);
        Assert.Equal("december.pdf", file.OriginalFilename);
        Assert.Equal(new string('a', 64), file.Sha256Hash);
        Assert.Equal(Auditee, file.UploadedBy);
        Assert.Equal(UploadedAt, file.UploadedAt);
        Assert.Equal("audits/x/evidence-requests/y/z", file.StoragePath);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_recall_must_be_explained(string? reason)
    {
        var file = NewFile();

        var ex = Assert.Throws<DomainException>(() => file.Recall(reason!, Auditee, Now));

        Assert.Equal("evidence.recall_reason_required", ex.Code);
        Assert.False(file.IsRecalled);
    }

    [Fact]
    public void An_overlong_reason_is_rejected()
    {
        var ex = Assert.Throws<DomainException>(
            () => NewFile().Recall(new string('x', EvidenceFile.MaxRecallReasonLength + 1), Auditee, Now));

        Assert.Equal("evidence.recall_reason_too_long", ex.Code);
    }

    [Fact]
    public void A_document_cannot_be_recalled_twice()
    {
        var file = NewFile();
        file.Recall("Wrong file", Auditee, Now);

        var ex = Assert.Throws<InvalidStateTransitionException>(() => file.Recall("Again", Auditee, Now.AddHours(1)));

        Assert.Equal("evidence.already_recalled", ex.Code);
        // The original recall stands unchanged.
        Assert.Equal("Wrong file", file.RecallReason);
        Assert.Equal(Now, file.RecalledAt);
    }

    [Theory]
    [InlineData(EvidenceContextType.Response)]
    [InlineData(EvidenceContextType.MapAction)]
    [InlineData(EvidenceContextType.SanctionsDossier)]
    public void Only_a_document_provided_for_an_evidence_request_can_be_recalled(EvidenceContextType context)
    {
        // Evidence an auditor attached to a response (or a MAP / dossier file) is the audit's own working paper.
        var ex = Assert.Throws<DomainException>(() => NewFile(context).Recall("Not allowed", Auditee, Now));

        Assert.Equal("evidence.recall_not_supported", ex.Code);
    }

    // ---- The request ----------------------------------------------------------------------------

    [Fact]
    public void Recalling_the_only_document_puts_the_request_back_to_outstanding_and_tells_the_auditor()
    {
        var request = NewRequest();
        request.RecordUpload(Auditee, UploadedAt);
        Assert.Equal(EvidenceRequestStatus.Received, request.Status);

        var reopened = request.RecordRecall("Branch Audit", "december.pdf", "Wrong month", Auditee, remainingDocuments: 0);

        Assert.True(reopened);
        Assert.Equal(EvidenceRequestStatus.Requested, request.Status);
        Assert.Null(request.ReceivedByUserId);
        Assert.Null(request.ReceivedAt);

        var raised = Assert.Single(request.DomainEvents.OfType<EvidenceRecalledEvent>());
        Assert.Equal(Auditor, raised.RequestedByUserId); // who is notified
        Assert.Equal(Auditee, raised.RecalledByUserId);
        Assert.Equal("december.pdf", raised.Filename);
        Assert.Equal("Wrong month", raised.Reason);
        Assert.True(raised.Reopened);
    }

    [Fact]
    public void Recalling_one_of_several_documents_leaves_the_request_received()
    {
        var request = NewRequest();
        request.RecordUpload(Auditee, UploadedAt);

        var reopened = request.RecordRecall("Branch Audit", "scan-1.pdf", "Superseded", Auditee, remainingDocuments: 1);

        Assert.False(reopened);
        Assert.Equal(EvidenceRequestStatus.Received, request.Status);
        Assert.NotNull(request.ReceivedAt);
        Assert.False(Assert.Single(request.DomainEvents.OfType<EvidenceRecalledEvent>()).Reopened);
    }

    [Fact]
    public void A_reopened_request_is_received_again_when_a_replacement_is_uploaded()
    {
        var request = NewRequest();
        request.RecordUpload(Auditee, UploadedAt);
        request.RecordRecall("Branch Audit", "december.pdf", "Wrong month", Auditee, remainingDocuments: 0);

        request.RecordUpload(Auditee, Now);

        Assert.Equal(EvidenceRequestStatus.Received, request.Status);
        Assert.Equal(Now, request.ReceivedAt);
    }
}

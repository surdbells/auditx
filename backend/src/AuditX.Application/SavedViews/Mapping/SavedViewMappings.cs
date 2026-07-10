using AuditX.Application.SavedViews.Dtos;
using AuditX.Domain.SavedViews;

namespace AuditX.Application.SavedViews.Mapping;

internal static class SavedViewMappings
{
    public static SavedViewDto ToDto(this SavedView view, Guid? requestingUserId) => new(
        view.Id,
        view.OwnerUserId,
        view.ViewKey,
        view.Name,
        view.ParametersJson,
        view.IsShared,
        requestingUserId is { } uid && view.OwnerUserId == uid,
        RowVersionToken.Encode(view.Version));
}

using Ecare.Application.Auth.Responses;
using Ecare.Application.Common.Responses;
using MediatR;

namespace Ecare.Application.Auth.Queries;

public record GetAllUsersQuery(
    int Page = 1,
    int PageSize = 10,
    string? UserName = null,
    string? Email = null,
    string? Prenom = null,
    string? Nom = null
) : IRequest<PagedResult<UserVm>>;

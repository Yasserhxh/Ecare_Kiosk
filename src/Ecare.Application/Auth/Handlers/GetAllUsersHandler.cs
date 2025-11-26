using Ecare.Application.Auth.Queries;
using Ecare.Application.Auth.Responses;
using Ecare.Application.Common.Responses;
using Ecare.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ecare.Application.Auth.Handlers;

public class GetAllUsersHandler(
    UserManager<ApplicationUser> userManager) : IRequestHandler<GetAllUsersQuery, PagedResult<UserVm>>
{
    public async Task<PagedResult<UserVm>> Handle(GetAllUsersQuery request, CancellationToken ct)
    {
        //Base query (filter by RaisonSociale by default)
        var query = userManager.Users
            .AsNoTracking()
            .Where(u => u.RaisonSociale == "ASMENT-TEMARA");

        

        if (!string.IsNullOrWhiteSpace(request.UserName))
        {
            var normalized = request.UserName.ToLower();
            query = query.Where(u => u.UserName!.ToLower().Contains(normalized));
        }

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var normalized = request.Email.ToLower();
            query = query.Where(u => u.Email != null && u.Email.ToLower().Contains(normalized));
        }

        if (!string.IsNullOrWhiteSpace(request.Prenom))
        {
            var normalized = request.Prenom.ToLower();
            query = query.Where(u => u.Prenom != null && u.Prenom.ToLower().Contains(normalized));
        }

        if (!string.IsNullOrWhiteSpace(request.Nom))
        {
            var normalized = request.Nom.ToLower();
            query = query.Where(u => u.Nom != null && u.Nom.ToLower().Contains(normalized));
        }

        // Total count
        var totalCount = await query.CountAsync(ct);

        // Pagination
        var skip = (request.Page - 1) * request.PageSize;
        var users = await query
            .OrderBy(u => u.UserName)
            .Skip(skip)
            .Take(request.PageSize)
            .ToListAsync(ct);

        var list = new List<UserVm>();

        foreach (var user in users)
        {
            var roles = await userManager.GetRolesAsync(user);

            list.Add(new UserVm
            {
                Id = user.Id,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                Prenom = user.Prenom,
                Nom = user.Nom,
                IsActive = user.IsActive,
                Roles = roles
            });
        }

        return new PagedResult<UserVm>
        {
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = totalCount,
            Items = list
        };
    }
}

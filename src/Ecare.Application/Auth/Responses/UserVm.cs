namespace Ecare.Application.Auth.Responses;

public class UserVm
{
    public string Id { get; set; }
    public string UserName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string? Prenom { get; set; }
    public string? Nom { get; set; }
    public bool IsActive { get; set; }
    public IList<string> Roles { get; set; } = new List<string>();
}

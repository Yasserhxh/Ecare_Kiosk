namespace Ecare.Application.Commands.GeneratePlombs
{
    public sealed record GeneratedPlombsVm(
        string Matricule,
        int NumberOfSeals,
        List<string> PlombNumbers
    );
}

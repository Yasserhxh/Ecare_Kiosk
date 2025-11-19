using Dapper;
using Ecare.Shared;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Ecare.Application.Commands.GeneratePlombs
{
    public sealed class GeneratePlombsHandler
        : IRequestHandler<GeneratePlombsCommand, Result<GeneratedPlombsVm>>
    {
        private readonly string _connString;

        public GeneratePlombsHandler(IConfiguration cfg)
        {
            _connString = cfg.GetConnectionString("SqlServer")
                         ?? throw new InvalidOperationException("Missing SqlServer connection string");
        }

        public async Task<Result<GeneratedPlombsVm>> Handle(
            GeneratePlombsCommand request,
            CancellationToken ct)
        {
            try
            {
                await using var conn = new SqlConnection(_connString);
                await conn.OpenAsync(ct);

                var rows = await conn.QueryAsync<PlombRow>(
                    "sp_Ecare_Truck_Plombs",
                    new { Matricule = request.Matricule },
                    commandType: System.Data.CommandType.StoredProcedure);

                var list = rows.ToList();

                if (list.Count == 0)
                    return Result<GeneratedPlombsVm>.Fail("No plombs generated");

                var vm = new GeneratedPlombsVm(
                    Matricule: request.Matricule,
                    NumberOfSeals: list.First().NumberOfSeals,
                    PlombNumbers: list.Select(x => x.PlombNumber).ToList()
                );

                return Result<GeneratedPlombsVm>.Ok(vm);
            }
            catch (Exception ex)
            {
                return Result<GeneratedPlombsVm>.Fail($"ERROR: {ex.Message}");
            }
        }

        private sealed class PlombRow
        {
            public string Matricule { get; init; } = default!;
            public int NumberOfSeals { get; init; }
            public string PlombNumber { get; init; } = default!;
        }
    }
}

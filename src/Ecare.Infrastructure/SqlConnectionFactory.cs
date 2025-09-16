using System.Data;
using Microsoft.Data.SqlClient;
using Ecare.Shared;

namespace Ecare.Infrastructure;
public sealed class SqlConnectionFactory(string connStr) : IDbConnectionFactory
{
    public IDbConnection Create() => new SqlConnection(connStr);
}

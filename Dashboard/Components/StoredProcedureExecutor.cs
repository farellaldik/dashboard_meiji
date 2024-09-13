using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace Dashboard.Components
{
    public static class StoredProcedureExecutor
    {
        public static async Task<T> ExecuteSPSingleAsync<T>(this DbContext context, string storedProcedureName, SqlParameter[] parameters)
        {
            T returnObject = default(T);

            using (var command = context.Database.GetDbConnection().CreateCommand())
            {
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = storedProcedureName;
                command.Parameters.AddRange(parameters);

                context.Database.OpenConnection();

                try
                {
                    var dataReader = await command.ExecuteReaderAsync();
                    returnObject = await dataReader.ToSingleAsync<T>();
                }
                catch (Exception)
                {
                    context.Database.CloseConnection();
                    throw;
                }

                context.Database.CloseConnection();
            }

            return returnObject;
        }

        public static T ExecuteSPSingle<T>(this DbContext context, string storedProcedureName, SqlParameter[] parameters)
        {
            T returnObject = default(T);

            using (var command = context.Database.GetDbConnection().CreateCommand())
            {
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = storedProcedureName;
                command.Parameters.AddRange(parameters);

                context.Database.OpenConnection();

                try
                {
                    var dataReader = command.ExecuteReader();
                    returnObject = SQLAutoMapper.ToSingle<T>(dataReader);
                }
                catch (Exception)
                {
                    context.Database.CloseConnection();
                    throw;
                }

                context.Database.CloseConnection();
            }

            return returnObject;
        }

        public static async Task<List<T>> ExecuteSPListAsync<T>(this DbContext context, string storedProcedureName, SqlParameter[] parameters)
        {
            var returnObject = new List<T>();

            using (var command = context.Database.GetDbConnection().CreateCommand())
            {
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = storedProcedureName;
                command.Parameters.AddRange(parameters);

                context.Database.OpenConnection();

                try
                {
                    var dataReader = await command.ExecuteReaderAsync();
                    returnObject = await dataReader.ToListAsync<T>();
                }
                catch (Exception)
                {
                    context.Database.CloseConnection();
                    throw;
                }

                context.Database.CloseConnection();
            }

            return returnObject;
        }

        public static List<T> ExecuteSPList<T>(this DbContext context, string storedProcedureName, SqlParameter[] parameters)
        {
            var returnObject = new List<T>();

            using (var command = context.Database.GetDbConnection().CreateCommand())
            {
                command.CommandType = CommandType.StoredProcedure;
                command.CommandTimeout = 999999;
                command.CommandText = storedProcedureName;
                command.Parameters.AddRange(parameters);
                context.Database.OpenConnection();

                try
                {
                    var dataReader = command.ExecuteReader();
                    returnObject = dataReader.ToList<T>();
                }
                catch (Exception)
                {
                    context.Database.CloseConnection();
                    throw;
                }

                context.Database.CloseConnection();
            }

            return returnObject;
        }
    }
}

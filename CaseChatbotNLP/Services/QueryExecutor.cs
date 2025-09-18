using Azure;
using CaseChatbotNLP.Data;
using CaseChatbotNLP.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace CaseChatbotNLP.Services
{
    public class QueryExecutor
    {
        private readonly DatabaseContext _context;

        public QueryExecutor(DatabaseContext context)
        {
            _context = context;
        }

        public string ExecuteQuery(string intentType, string sede, string responsable, string estado)
        {
            IQueryable<Caso> query = _context.Casos;

            if (!string.IsNullOrEmpty(sede))
                query = query.Where(c => c.Sede == sede);

            if (!string.IsNullOrEmpty(responsable))
                query = query.Where(c => c.Responsable.Contains(responsable));

            if (!string.IsNullOrEmpty(estado))
                query = query.Where(c => c.Estado == estado);

            switch (intentType)
            {
                case "CountBySede":
                    return $"Hay {query.Count()} casos en {sede}";

                case "QueryBySede":
                    var casos = query.Take(5).ToList();
                    return string.Join("\n", casos.Select(c => $"{c.NumeroCaso}: {c.Descripcion}"));

                default:
                    return $"Se encontraron {query.Count()} casos con los filtros aplicados";
            }
        }
        public string ExecuteQuery(string QuerySql)
        {
            string _respusta = "";
            using var connection = _context.Database.GetDbConnection();
            connection.Open();
            IDbCommand _ComandoDatos;
            _ComandoDatos = connection.CreateCommand();
            _ComandoDatos.CommandType = CommandType.Text;
            _ComandoDatos.CommandText = QuerySql;
            IDataReader DR = (IDataReader)_ComandoDatos.ExecuteReader();


            if (DR.Read())
            {
                int total = DR.GetSchemaTable().Rows.Count;
                _respusta += "[{";
                for (int i = 0; i < total; i++)
                {
                    _respusta += "'" + DR.GetSchemaTable().Rows[i].ItemArray[0] + "'";
                    _respusta += ":";
                    if ((total - i) == 1)
                        _respusta += " '" + DR[i] + " '";
                    else
                        _respusta += " '" + DR[i] + " ',";
                }
                _respusta += "}";

                while (DR.Read())
                {
                    _respusta += ",{";
                    for (int i = 0; i < total; i++)
                    {
                        _respusta += "'" + DR.GetSchemaTable().Rows[i].ItemArray[0] + "'";
                        _respusta += ":";
                        if ((total - i) == 1)
                            _respusta += " '" + DR[i].ToString().Replace("\"", "") + " '";
                        else
                            _respusta += " '" + DR[i].ToString().Replace("\"", "") + " ',";
                    }
                    _respusta += "}";
                }
                _respusta += "]";
            }

            DR.Close();
            return _respusta;

        }
    }
}
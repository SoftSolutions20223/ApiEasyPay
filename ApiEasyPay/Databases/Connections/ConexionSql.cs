using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Data;
using System.Text;

namespace ApiEasyPay.Databases.Connections
{
    public class ConexionSql
    {
        public string BdPrincipal = "";
        public string BdCliente = "";

        public string CreaCadenaConexServ(string Servidor, string Bd, string usu, string pw)
        {
            return $"Data Source={Servidor};Database={Bd};User Id={usu};Password={pw};Encrypt=false;";
        }

        public string Conection(bool Principal)
        {
            return Principal ? BdPrincipal : BdCliente;
        }

        public DataTable SqlConsulta(string SQL, bool bd)
        {
            SQL = "set dateformat dmy; " + SQL;
            SqlConnection conec = new SqlConnection(Conection(bd));
            //SqlConnection conec = new SqlConnection(@"Data Source=LAPTOP-3JUTLUGJ\SQLEXPRESS;Database=Fily_Usuarios;User Id=sa;Password=8213;Encrypt=false;");
            SqlCommand comando = new SqlCommand(SQL, conec);
            SqlDataAdapter datos = new SqlDataAdapter(comando);
            DataTable tabla = new DataTable();

            try
            {
                conec.Open();
                datos.Fill(tabla);
            }
            catch (Exception e)
            {
                return null;
            }
            finally
            {
                conec.Close();
                comando.Dispose();
            }
            return tabla;
        }

        public object TraerDato(string SQL, bool bd)
        {
            SQL = "set dateformat dmy; " + SQL;
            SqlConnection conec = new SqlConnection(Conection(bd));
            SqlCommand comando = new SqlCommand(SQL, conec);
            SqlDataAdapter datos = new SqlDataAdapter(comando);
            DataTable tabla = new DataTable();

            try
            {
                conec.Open();
                datos.Fill(tabla);
            }
            catch (Exception e)
            {
                return null;
            }
            finally
            {
                conec.Close();
                comando.Dispose();
            }
            return tabla.Rows[0][0];
        }

        public List<string> Listar2(string sql, bool bd)
        {
            List<string> lista = new List<string>();
            try
            {
                SqlConnection conec = new SqlConnection(Conection(bd));
                conec.Open();
                SqlCommand comando = new SqlCommand(sql, conec);
                SqlDataAdapter datos = new SqlDataAdapter(comando);
                IDataReader reader = comando.ExecuteReader();

                while (reader.Read())
                {
                    lista.Add(reader.GetString(0));
                }
                conec.Close();
                return lista;
            }
            catch (Exception ex)
            {
                Console.Write(ex);
                return null;
            }
        }

        public string[] arrays(string sql, bool bd)
        {
            try
            {
                SqlConnection conec = new SqlConnection(Conection(bd));
                conec.Open();
                SqlCommand comando = new SqlCommand(sql, conec);
                SqlDataAdapter datos = new SqlDataAdapter(comando);
                DataTable tabla = new DataTable();
                datos.Fill(tabla);
                string[] lista = new string[tabla.Rows.Count * 2];
                int conte2 = 0;
                for (int conte = 0; conte < tabla.Rows.Count; conte++)
                {
                    lista[conte2] = tabla.Rows[conte]["nombre"].ToString();
                    lista[conte2 + 1] = tabla.Rows[conte]["cantidad"].ToString();
                    conte2 += 2;
                }
                conec.Close();
                return lista;
            }
            catch (Exception ex)
            {
                Console.Write(ex);
                return null;
            }
        }

        public List<object> Listar(string sql, bool bd)
        {
            List<object> lista = new List<object>();
            lista.Add(new object[] { "nombre", "cantidad" });

            try
            {
                SqlConnection conec = new SqlConnection(Conection(bd));
                conec.Open();
                SqlCommand comando = new SqlCommand(sql, conec);
                SqlDataAdapter datos = new SqlDataAdapter(comando);
                IDataReader reader = comando.ExecuteReader();
                while (reader.Read())
                {
                    lista.Add(new object[] { reader["nombre"], reader["cantidad"] });
                }
                conec.Close();
                return lista;
            }
            catch (Exception ex)
            {
                Console.Write(ex);
                return null;
            }
        }

        public string SqlQueryGestion(string SQL, bool bd)
        {
            SqlConnection conexion = new SqlConnection(Conection(bd));
            SqlCommand comando = new SqlCommand("Set dateformat dmy; " + SQL);
            comando.Connection = conexion;
            string val = "no";

            try
            {
                conexion.Open();
                int fill_afec = comando.ExecuteNonQuery();

                if (fill_afec > 0)
                {
                    val = "yes";
                }
            }
            catch (Exception a)
            {
                return a.Message.ToString();
            }
            finally
            {
                conexion.Close();
                comando.Dispose();
            }
            return val;
        }

        public string SqlJsonComand2(SqlCommand comando, SqlConnection conec)
        {
            comando.Connection = conec;
            var jsonResult = new StringBuilder();
            try
            {
                using (var reader = comando.ExecuteReader(CommandBehavior.Default))
                {
                    if (!reader.HasRows)
                    {
                        jsonResult.Append("[]");
                    }
                    else
                    {
                        while (reader.Read())
                        {
                            jsonResult.Append(reader.GetValue(0).ToString());
                        }
                    }
                }
            }
            catch (Exception e)
            {
                jsonResult.Append("[]");
            }
            return jsonResult.ToString();
        }

        public string SqlJsonComand(bool bd, SqlCommand comando)
        {
            SqlConnection conec = new SqlConnection(Conection(bd));
            comando.Connection = conec;
            var jsonResult = new StringBuilder();
            string commandString = comando.CommandText;
            foreach (SqlParameter param in comando.Parameters)
            {
                string paramValue = param.Value?.ToString() ?? "NULL";
                commandString = commandString.Replace(param.ParameterName, paramValue);
            }


            try
            {
                conec.Open();
                var reader = comando.ExecuteReader();
                if (!reader.HasRows)
                {
                    jsonResult.Append("[]");
                }
                else
                {
                    while (reader.Read())
                    {
                        jsonResult.Append(reader.GetValue(0).ToString());
                    }
                }
            }
            catch (Exception e)
            {
                jsonResult.Append("{\"msg\":" + e.Message + "}");
            }
            finally
            {
                conec.Close();
                comando.Dispose();
            }

            return jsonResult.ToString();
        }


        public JArray SqlJsonCommandArray(bool bd, SqlCommand comando)
        {
            SqlConnection conec = new SqlConnection(Conection(bd));
            comando.Connection = conec;
            JArray resultArray = new JArray();

            try
            {
                conec.Open();
                var reader = comando.ExecuteReader();

                if (!reader.HasRows)
                    return resultArray;

                while (reader.Read())
                {
                    string jsonStr = reader.GetValue(0).ToString();
                    if (!string.IsNullOrEmpty(jsonStr))
                    {
                        try
                        {
                            // Si es objeto
                            if (jsonStr.TrimStart().StartsWith("{"))
                            {
                                JObject jObject = JObject.Parse(jsonStr);
                                resultArray.Add(jObject);
                            }
                            // Si ya es array
                            else if (jsonStr.TrimStart().StartsWith("["))
                            {
                                JArray parsedArray = JArray.Parse(jsonStr);
                                foreach (var item in parsedArray)
                                    resultArray.Add(item);
                            }
                        }
                        catch (JsonReaderException)
                        {
                            resultArray.Add(new JValue(jsonStr));
                        }
                    }
                }
            }
            catch (Exception e)
            {
                JObject errorObj = new JObject
                {
                    ["MensajeError"] = e.Message,
                    ["FuncionOrigen"] = "SqlJsonCommandArray",
                    ["ConsultaSQL"] = comando.CommandText,
                    ["TipoError"] = e.GetType().Name
                };
                resultArray.Add(errorObj);
            }
            finally
            {
                conec.Close();
                comando.Dispose();
            }

            return resultArray;
        }

        public JObject SqlJsonCommandObject(bool bd, SqlCommand comando)
        {
            SqlConnection conec = new SqlConnection(Conection(bd));
            comando.Connection = conec;
            JObject resultObject = new JObject();

            try
            {
                conec.Open();
                var reader = comando.ExecuteReader();

                if (reader.HasRows && reader.Read())
                {
                    string jsonStr = reader.GetValue(0).ToString();
                    if (!string.IsNullOrEmpty(jsonStr))
                    {
                        resultObject = JObject.Parse(jsonStr);
                    }
                }
            }
            catch (Exception e)
            {
                resultObject = new JObject
                {
                    ["MensajeError"] = e.Message,
                    ["FuncionOrigen"] = "SqlJsonCommandObject",
                    ["ConsultaSQL"] = comando.CommandText,
                    ["Parametros"] = new JObject(comando.Parameters.Cast<SqlParameter>().Select(p =>
                        new JProperty(p.ParameterName, p.Value?.ToString() ?? "NULL"))),
                    ["TipoError"] = e.GetType().Name
                };
            }
            finally
            {
                conec.Close();
                comando.Dispose();
            }

            return resultObject;
        }

        public string SqlJson(string comandoSQL, bool bd)
        {
            comandoSQL = "set dateformat dmy; " + comandoSQL;
            SqlConnection conec = new SqlConnection(Conection(bd));
            SqlCommand comando = new SqlCommand(comandoSQL, conec);
            var jsonResult = new StringBuilder();
            try
            {
                conec.Open();
                var reader = comando.ExecuteReader();
                if (!reader.HasRows)
                {
                    jsonResult.Append("[]");
                }
                else
                {
                    while (reader.Read())
                    {
                        jsonResult.Append(reader.GetValue(0).ToString());
                    }
                }
            }
            catch (Exception e)
            {
                jsonResult.Append("[]");
            }
            finally
            {
                conec.Close();
                comando.Dispose();
            }

            return jsonResult.ToString();
        }
    }
}

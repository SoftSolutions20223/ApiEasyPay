using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using Newtonsoft.Json.Linq;

namespace ApiEasyPay.Databases.Connections
{
    public class ConexionMongo
    {
        private readonly IMongoDatabase _database;
        private readonly IMongoCollection<BsonDocument> _sessionCollection;

        /// <summary>
        /// Constructor que inicializa la conexión a MongoDB.
        /// Recibe la cadena de conexión y el nombre de la base de datos.
        /// Si la colección "sessions" no existe, la crea.
        /// </summary>
        /// <param name="connectionString">Cadena de conexión a MongoDB.</param>
        /// <param name="databaseName">Nombre de la base de datos.</param>
        public ConexionMongo(string connectionString, string databaseName)
        {
            var client = new MongoClient(connectionString);
            _database = client.GetDatabase(databaseName);

            // Verifica si la colección "sessions" existe; si no, la crea.
            var collectionNames = _database.ListCollectionNames().ToList();
            if (!collectionNames.Contains("sessions"))
            {
                _database.CreateCollection("sessions");
            }
            _sessionCollection = _database.GetCollection<BsonDocument>("sessions");

            // Crea un índice único en el campo "Token" para optimizar las búsquedas.
            CreateIndexes();
        }

        /// <summary>
        /// Crea índices necesarios, por ejemplo, un índice único en el campo "Token".
        /// </summary>
        private void CreateIndexes()
        {
            var tokenIndex = Builders<BsonDocument>.IndexKeys.Ascending("Token");
            _sessionCollection.Indexes.CreateOne(new CreateIndexModel<BsonDocument>(
                tokenIndex, new CreateIndexOptions { Unique = true }));
        }

        /// <summary>
        /// Inserta o actualiza (upsert) una sesión en la colección.
        /// Se espera que el JObject incluya el campo "Token" en su contenido.
        /// Se agregan las marcas de tiempo "CreatedAt" y "UpdatedAt".
        /// </summary>
        /// <param name="json">El objeto JSON (JObject) con los datos de sesión.</param>
        public async Task InsertOrUpdateSessionAsync(JObject json)
        {
            string token = json["Token"]?.ToString();
            string username = json["Usuario"]?.ToString(); // Suponiendo que "Usu" contiene el nombre de usuario

            if (string.IsNullOrEmpty(token))
            {
                throw new ArgumentException("El JSON debe contener el campo 'Token'.");
            }

            if (!string.IsNullOrEmpty(username))
            {
                // Primero eliminamos todas las sesiones existentes para este usuario
                var filterUser = Builders<BsonDocument>.Filter.Eq("Usuario", username);
                await _sessionCollection.DeleteManyAsync(filterUser);
            }

            // Convertir el JObject a BsonDocument
            var document = BsonDocument.Parse(json.ToString());
            document["CreatedAt"] = DateTime.UtcNow;
            document["UpdatedAt"] = DateTime.UtcNow;

            // Insertar nuevo documento
            await _sessionCollection.InsertOneAsync(document);
        }

        /// <summary>
        /// Consulta una sesión a partir del token.
        /// Devuelve el documento como JObject o null si no se encuentra.
        /// </summary>
        /// <param name="token">El token de la sesión.</param>
        public async Task<JObject> GetSessionByTokenAsync(string token)
        {
            var filter = Builders<BsonDocument>.Filter.Eq("Token", token);
            var document = await _sessionCollection.Find(filter).FirstOrDefaultAsync();
            return document != null ? JObject.Parse(document.ToJson()) : null;
        }

        /// <summary>
        /// Actualiza el contenido completo de una sesión identificado por el token.
        /// Se reemplaza el documento (excepto el _id) y se actualiza la marca de tiempo "UpdatedAt".
        /// </summary>
        /// <param name="token">El token de la sesión.</param>
        /// <param name="newJson">El nuevo contenido de la sesión en formato JObject.</param>
        public async Task UpdateSessionAsync(string token, JObject newJson)
        {
            if (string.IsNullOrEmpty(token))
            {
                throw new ArgumentException("El token es requerido para actualizar la sesión.");
            }

            // Asegurarse de que el token esté presente en el nuevo JSON
            if (newJson["Token"] == null)
            {
                newJson["Token"] = token;
            }

            var document = BsonDocument.Parse(newJson.ToString());
            document["UpdatedAt"] = DateTime.UtcNow;

            var filter = Builders<BsonDocument>.Filter.Eq("Token", token);
            await _sessionCollection.ReplaceOneAsync(filter, document);
        }

        /// <summary>
        /// Elimina una sesión a partir del token.
        /// </summary>
        /// <param name="token">El token de la sesión a eliminar.</param>
        public async Task DeleteSessionAsync(string token)
        {
            var filter = Builders<BsonDocument>.Filter.Eq("Token", token);
            await _sessionCollection.DeleteOneAsync(filter);
        }

        /// <summary>
        /// Recupera todas las sesiones almacenadas, devolviéndolas como una lista de JObject.
        /// </summary>
        public async Task<List<JObject>> GetAllSessionsAsync()
        {
            var documents = await _sessionCollection.Find(new BsonDocument()).ToListAsync();
            return documents.Select(doc => JObject.Parse(doc.ToJson())).ToList();
        }
    }
}
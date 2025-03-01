using Newtonsoft.Json.Linq;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace ApiEasyPay.Helpers
{
    public class CustomJsonDeserializer
    {
        /// <summary>
        /// JArray que contendrá todos los errores encontrados durante la deserialización.
        /// </summary>
        public JArray Errors { get; private set; } = new JArray();

        /// <summary>
        /// Deserializa un JObject al tipo T, validando cada propiedad según los atributos de DataAnnotations
        /// definidos en la clase (como [Required], [MaxLength], [Range], [DisplayFormat], etc.).
        /// En caso de error, se registra el fallo pero no se sobrescribe el valor ya asignado en la instancia.
        /// Se retorna siempre una instancia de T, y el llamador deberá revisar la propiedad Errors.
        /// </summary>
        /// <typeparam name="T">Tipo destino (debe tener constructor por defecto y, opcionalmente, valores iniciales).</typeparam>
        /// <param name="json">El JObject que contiene los datos.</param>
        /// <returns>Instancia de T con los valores asignados o manteniendo los valores por defecto definidos en la clase.</returns>
        public T Deserialize<T>(JObject json) where T : new()
        {
            // Reiniciamos la colección de errores y creamos la instancia destino.
            Errors = new JArray();
            T obj = new T();

            // Recorremos cada propiedad pública de la clase destino.
            foreach (PropertyInfo property in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                string propName = property.Name;
                JToken token;

                // Buscamos la propiedad en el JSON (ignorando mayúsculas/minúsculas).
                bool hasToken = json.TryGetValue(propName, StringComparison.OrdinalIgnoreCase, out token);

                // Obtenemos el atributo [Required], si lo hay.
                var requiredAttr = property.GetCustomAttribute<RequiredAttribute>();

                // Si no se encontró el token y la propiedad es requerida, se registra el error.
                if (!hasToken)
                {
                    if (requiredAttr != null)
                    {
                        Errors.Add(new JObject
                        {
                            ["Property"] = propName,
                            ["ErrorMessage"] = "La propiedad es requerida pero no se encontró en el JSON."
                        });
                    }
                    continue;
                }

                // Si el token es null y la propiedad es requerida, se registra el error.
                if (token.Type == JTokenType.Null)
                {
                    if (requiredAttr != null)
                    {
                        Errors.Add(new JObject
                        {
                            ["Property"] = propName,
                            ["ErrorMessage"] = "La propiedad es requerida y no puede ser null."
                        });
                    }
                    continue;
                }

                try
                {
                    // Si la propiedad es de tipo string, se realizan validaciones específicas.
                    if (property.PropertyType == typeof(string))
                    {
                        string value = token.ToObject<string>();

                        // Si es requerida, se valida que no sea cadena vacía.
                        if (requiredAttr != null && string.IsNullOrEmpty(value))
                        {
                            Errors.Add(new JObject
                            {
                                ["Property"] = propName,
                                ["ErrorMessage"] = "La propiedad es requerida y no puede estar vacía."
                            });
                            // No se asigna; se mantiene el valor inicial (definido en la clase) o null.
                            continue;
                        }

                        // Si existe [MaxLength], se valida que la longitud no exceda el máximo permitido.
                        var maxLengthAttr = property.GetCustomAttribute<MaxLengthAttribute>();
                        if (maxLengthAttr != null && value != null && value.Length > maxLengthAttr.Length)
                        {
                            Errors.Add(new JObject
                            {
                                ["Property"] = propName,
                                ["ErrorMessage"] = $"La longitud de la propiedad excede el máximo permitido ({maxLengthAttr.Length})."
                            });
                            continue;
                        }

                        property.SetValue(obj, value);
                    }
                    else
                    {
                        // Para otros tipos se intenta convertir el token al tipo de la propiedad.
                        object value = token.ToObject(property.PropertyType);

                        // Si existe [Range], se valida que el valor esté dentro del rango permitido.
                        var rangeAttr = property.GetCustomAttribute<RangeAttribute>();
                        if (rangeAttr != null && value is IComparable compValue)
                        {
                            if (compValue.CompareTo(rangeAttr.Minimum) < 0 || compValue.CompareTo(rangeAttr.Maximum) > 0)
                            {
                                Errors.Add(new JObject
                                {
                                    ["Property"] = propName,
                                    ["ErrorMessage"] = $"El valor de la propiedad está fuera del rango permitido ({rangeAttr.Minimum} - {rangeAttr.Maximum})."
                                });
                                continue;
                            }
                        }

                        property.SetValue(obj, value);
                    }
                }
                catch (Exception ex)
                {
                    // Si ocurre un error durante la conversión, se registra el error y se deja el valor inicial.
                    Errors.Add(new JObject
                    {
                        ["Property"] = propName,
                        ["ErrorMessage"] = $"Error al convertir la propiedad: {ex.Message}"
                    });
                }
            }

            // Validación global del objeto usando DataAnnotations.
            var validationContext = new ValidationContext(obj, serviceProvider: null, items: null);
            var validationResults = new List<ValidationResult>();
            bool isValid = Validator.TryValidateObject(obj, validationContext, validationResults, true);

            if (!isValid)
            {
                foreach (var validationResult in validationResults)
                {
                    foreach (var memberName in validationResult.MemberNames)
                    {
                        Errors.Add(new JObject
                        {
                            ["Property"] = memberName,
                            ["ErrorMessage"] = validationResult.ErrorMessage
                        });
                    }
                }
            }

            // Se retorna siempre el objeto; el llamador deberá revisar si Errors está vacío para saber si la deserialización fue completamente correcta.
            return obj;
        }

        /// <summary>
        /// Método de deserialización no genérico que recibe el Type destino y un JObject.
        /// Permite invocar el método genérico anterior mediante reflection.
        /// </summary>
        /// <param name="type">Tipo destino.</param>
        /// <param name="json">JObject con los datos.</param>
        /// <returns>Instancia del tipo indicado con valores ya establecidos (o los iniciales) y con errores registrados en Errors si los hubiera.</returns>
        public object Deserialize(Type type, JObject json)
        {
            MethodInfo method = typeof(CustomJsonDeserializer)
                                    .GetMethod(nameof(Deserialize), new Type[] { typeof(JObject) })
                                    .MakeGenericMethod(type);
            return method.Invoke(this, new object[] { json });
        }
    }
}
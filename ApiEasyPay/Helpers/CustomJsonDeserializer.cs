﻿using Newtonsoft.Json.Linq;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Collections.Generic;

namespace ApiEasyPay.Helpers
{
    public class CustomJsonDeserializer
    {
        /// <summary>
        /// JArray que contendrá todos los errores encontrados durante la deserialización.
        /// </summary>
        public JArray Errors { get; private set; } = new JArray();

        /// <summary>
        /// Deserializa un JObject al tipo T, validando cada propiedad según los atributos de DataAnnotations.
        /// En caso de error, se registra el fallo pero no se sobrescribe el valor ya asignado en la instancia.
        /// Se retorna siempre una instancia de T y el llamador deberá revisar la propiedad Errors.
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
                    // Manejo especial para enteros
                    if (property.PropertyType == typeof(int) || property.PropertyType == typeof(int?))
                    {
                        if (token.Type == JTokenType.Integer)
                        {
                            // Si ya es entero, asignación directa
                            property.SetValue(obj, token.ToObject<int>());
                        }
                        else if (token.Type == JTokenType.Float)
                        {
                            // Convierte double a int si no tiene parte decimal
                            double doubleValue = token.ToObject<double>();
                            if (Math.Abs(doubleValue - Math.Floor(doubleValue)) < 0.00001)
                            {
                                property.SetValue(obj, Convert.ToInt32(doubleValue));
                            }
                            else
                            {
                                Errors.Add(new JObject
                                {
                                    ["Property"] = propName,
                                    ["ErrorMessage"] = $"El valor {doubleValue} contiene decimales y no puede ser convertido a entero."
                                });
                                continue;
                            }
                        }
                        else if (token.Type == JTokenType.String && int.TryParse(token.ToString(), out int intValue))
                        {
                            // Convierte strings numéricas a enteros
                            property.SetValue(obj, intValue);
                        }
                        else
                        {
                            Errors.Add(new JObject
                            {
                                ["Property"] = propName,
                                ["ErrorMessage"] = $"No se puede convertir el valor '{token}' a entero."
                            });
                            continue;
                        }

                        // Verificar el rango para enteros
                        var rangeAttr = property.GetCustomAttribute<RangeAttribute>();
                        if (rangeAttr != null)
                        {
                            int value = (int)property.GetValue(obj);
                        }
                    }
                    // Si la propiedad es de tipo string, se realizan validaciones específicas.
                    else if (property.PropertyType == typeof(string))
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
                    else if (property.PropertyType == typeof(DateTime) || property.PropertyType == typeof(DateTime?))
                    {
                        if (token.Type == JTokenType.String)
                        {
                            string dateStr = token.ToString();
                            DateTime dateValue;

                            // Intentar con varios formatos de fecha, comenzando con dd/MM/yyyy
                            if (DateTime.TryParseExact(dateStr,
                                                      new[] { "dd/MM/yyyy", "yyyy-MM-dd", "MM/dd/yyyy" },
                                                      System.Globalization.CultureInfo.InvariantCulture,
                                                      System.Globalization.DateTimeStyles.None,
                                                      out dateValue))
                            {
                                property.SetValue(obj, dateValue);
                            }
                            else if (DateTime.TryParse(dateStr, out dateValue))
                            {
                                property.SetValue(obj, dateValue);
                            }
                            else
                            {
                                Errors.Add(new JObject
                                {
                                    ["Property"] = propName,
                                    ["ErrorMessage"] = $"Error al convertir la propiedad: String '{dateStr}' was not recognized as a valid DateTime."
                                });
                                continue;
                            }
                        }
                        else
                        {
                            try
                            {
                                object value = token.ToObject(property.PropertyType);
                                property.SetValue(obj, value);
                            }
                            catch (Exception ex)
                            {
                                Errors.Add(new JObject
                                {
                                    ["Property"] = propName,
                                    ["ErrorMessage"] = $"No se puede convertir el valor '{token}' a fecha: {ex.Message}"
                                });
                                continue;
                            }
                        }
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
        /// <returns>Instancia del tipo indicado con valores ya establecidos y errores registrados en Errors si los hubiera.</returns>
        public object Deserialize(Type type, JObject json)
        {
            MethodInfo method = typeof(CustomJsonDeserializer)
                                    .GetMethod(nameof(Deserialize), new Type[] { typeof(JObject) })
                                    .MakeGenericMethod(type);
            return method.Invoke(this, new object[] { json });
        }

        /// <summary>
        /// Deserializa un JArray a una lista de objetos de tipo T, aplicando el mismo proceso de validación que se realiza para un solo objeto.
        /// Cada elemento del arreglo se procesa individualmente y se acumulan los errores, asociándolos al índice correspondiente.
        /// </summary>
        /// <typeparam name="T">Tipo destino de cada elemento (debe tener constructor por defecto).</typeparam>
        /// <param name="jsonArray">JArray que contiene los objetos JSON.</param>
        /// <returns>Lista de instancias de T deserializadas.</returns>
        public List<T> DeserializeList<T>(JArray jsonArray) where T : new()
        {
            // Reiniciamos los errores globales.
            Errors = new JArray();
            List<T> resultList = new List<T>();

            for (int i = 0; i < jsonArray.Count; i++)
            {
                // Verificamos que el elemento sea un JObject.
                if (jsonArray[i] is JObject obj)
                {
                    // Para cada elemento, se crea una instancia separada del deserializador
                    // para evitar que la propiedad Errors se reinicie en cada llamada.
                    CustomJsonDeserializer elementDeserializer = new CustomJsonDeserializer();
                    T deserializedObject = elementDeserializer.Deserialize<T>(obj);

                    // Se agregan los errores de la deserialización del elemento, indicando el índice.
                    if (elementDeserializer.Errors.Count > 0)
                    {
                        foreach (JObject error in elementDeserializer.Errors)
                        {
                            error["Index"] = i;
                            Errors.Add(error);
                        }
                    }

                    resultList.Add(deserializedObject);
                }
                else
                {
                    // Si el elemento no es un objeto JSON válido, se registra el error.
                    Errors.Add(new JObject
                    {
                        ["Index"] = i,
                        ["ErrorMessage"] = "El elemento no es un objeto JSON."
                    });
                }
            }

            return resultList;
        }

        /// <summary>
        /// Método de deserialización no genérico para JArray que recibe el Type destino.
        /// Permite invocar el método genérico anterior mediante reflection.
        /// </summary>
        /// <param name="type">Tipo destino de cada elemento.</param>
        /// <param name="jsonArray">JArray con los datos.</param>
        /// <returns>Lista de instancias del tipo indicado con valores ya establecidos y errores registrados en Errors si los hubiera.</returns>
        public object DeserializeList(Type type, JArray jsonArray)
        {
            MethodInfo method = typeof(CustomJsonDeserializer)
                                    .GetMethod(nameof(DeserializeList), new Type[] { typeof(JArray) })
                                    .MakeGenericMethod(type);
            return method.Invoke(this, new object[] { jsonArray });
        }
    }
}

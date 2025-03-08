using Newtonsoft.Json.Linq;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace ApiEasyPay.Helpers
{
    public class CustomJsonSerializer
    {
        /// <summary>
        /// Serializa una instancia de T a un JObject, aplicando el DisplayFormat de cada propiedad.
        /// </summary>
        /// <typeparam name="T">Tipo del objeto a serializar.</typeparam>
        /// <param name="obj">Instancia del objeto.</param>
        /// <returns>JObject con los datos serializados.</returns>
        public JObject Serialize<T>(T obj)
        {
            JObject json = new JObject();
            if (obj == null)
                return json;

            foreach (PropertyInfo property in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                string propName = property.Name;
                object value = property.GetValue(obj);

                // Verificamos si la propiedad tiene un DisplayFormatAttribute definido.
                var displayFormatAttr = property.GetCustomAttribute<DisplayFormatAttribute>();

                if (value != null && displayFormatAttr != null &&
                    !string.IsNullOrEmpty(displayFormatAttr.DataFormatString))
                {
                    // Se formatea el valor según el DataFormatString.
                    string formattedValue = string.Format(displayFormatAttr.DataFormatString, value);
                    json[propName] = formattedValue;
                }
                else
                {
                    // Manejo especial para strings con caracteres especiales
                    if (value is string stringValue)
                    {
                        json[propName] = stringValue; // JsonConvert manejará la codificación
                    }
                    else
                    {
                        // Si no hay DisplayFormat o el valor es null, se asigna normalmente.
                        JToken token = value != null ? JToken.FromObject(value) : JValue.CreateNull();
                        json[propName] = token;
                    }
                }
            }

            return json;
        }

        /// <summary>
        /// Método no genérico para serializar un objeto a JObject.
        /// </summary>
        /// <param name="obj">El objeto a serializar.</param>
        /// <returns>JObject con los datos serializados.</returns>
        public JObject Serialize(object obj)
        {
            if (obj == null)
                return new JObject();

            Type type = obj.GetType();
            JObject json = new JObject();

            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                string propName = property.Name;
                object value = property.GetValue(obj);

                var displayFormatAttr = property.GetCustomAttribute<DisplayFormatAttribute>();

                if (value != null && displayFormatAttr != null &&
                    !string.IsNullOrEmpty(displayFormatAttr.DataFormatString))
                {
                    string formattedValue = string.Format(displayFormatAttr.DataFormatString, value);
                    json[propName] = formattedValue;
                }
                else
                {
                    JToken token = value != null ? JToken.FromObject(value) : JValue.CreateNull();
                    json[propName] = token;
                }
            }

            return json;
        }

        /// <summary>
        /// Serializa una colección de instancias de T a un JArray, aplicando el proceso de formateo.
        /// </summary>
        /// <typeparam name="T">Tipo de los objetos a serializar.</typeparam>
        /// <param name="list">Colección de objetos.</param>
        /// <returns>JArray con los objetos serializados.</returns>
        public JArray SerializeList<T>(IEnumerable<T> list)
        {
            JArray array = new JArray();
            if (list == null)
                return array;

            foreach (T item in list)
            {
                JObject jsonObject = Serialize(item);
                array.Add(jsonObject);
            }
            return array;
        }

        /// <summary>
        /// Método no genérico para serializar una colección de objetos a un JArray.
        /// </summary>
        /// <param name="list">Colección de objetos a serializar.</param>
        /// <returns>JArray con los objetos serializados.</returns>
        public JArray SerializeList(IEnumerable<object> list)
        {
            JArray array = new JArray();
            if (list == null)
                return array;

            foreach (object item in list)
            {
                JObject jsonObject = Serialize(item);
                array.Add(jsonObject);
            }
            return array;
        }
    }
}
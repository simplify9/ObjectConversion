using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using SW.PrimitiveTypes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Cache;
using System.Text;

namespace SW.ObjectConversion
{
    public static class QueryStringHelpers
    {
        public static string GetQueryStringFromObject<T>(T obj) where T : class
        {
            string[] props = typeof(T).GetProperties()
                            .Select(property => {

                                bool isArray = property.PropertyType.IsArray;
                                bool isCollection = property.PropertyType.GetInterfaces()
                                                    .Where(i => i.IsGenericType)
                                                    .Select(i => i.GetGenericTypeDefinition())
                                                    .Contains(typeof(ICollection<>));
                                if (isArray || isCollection)
                                {
                                    IEnumerable tmp = ((IEnumerable)property.GetValue(obj).ConvertValueToType(property.PropertyType));
                                    IEnumerable<string> enumerable = tmp.Cast<string>();

                                    Type nested = property.PropertyType.GetElementType() ?? property.PropertyType.GetGenericArguments()[0];

                                    int length = 0;
                                    foreach (var val in tmp) ++length;

                                    Array array = Array.CreateInstance(nested, length);

                                    int counter = 0;
                                    foreach (var val in tmp)
                                    {
                                        array.SetValue(val, counter);
                                        ++counter;
                                    }

                                    string q = string.Empty;
                                    for (int i = 0; i < array.Length - 1; i++) q += $"{property.Name}={array.GetValue(i)}&";
                                    q += $"{property.Name}={array.GetValue(array.Length - 1)}";

                                    return q;
                                }
                                else return $"{property.Name}={property.GetValue(obj)}";
                            })
                            .ToArray();

            string queryString = string.Empty;
            for (int i = 0; i < props.Count() - 1; i++) queryString += props[i] + '&';
            queryString += props[props.Length - 1];

            return queryString;
        }

        public static object GetObjectFromQueryString(Type type, HttpRequest httpRequest)
        {
            try
            {
                var obj = Activator.CreateInstance(type);
                var properties = type.GetProperties();
                foreach (var property in properties)
                {
                    object value = null;
                    StringValues queries = httpRequest.Query[property.Name];

                    bool isArray = property.PropertyType.IsArray;
                    bool isCollection = property.PropertyType.GetInterfaces()
                                        .Where(i => i.IsGenericType)
                                        .Select(i => i.GetGenericTypeDefinition())
                                        .Contains(typeof(ICollection<>));

                    if (isArray || isCollection)
                    {
                        Type nested = property.PropertyType.GetElementType() ?? property.PropertyType.GetGenericArguments()[0];
                        Array tmp = Array.CreateInstance(nested, queries.Count);
                        var queryObjects = queries.Select(q => q.ConvertValueToType(nested));
                        for (int i = 0; i < queries.Count; i++)
                            tmp.SetValue(queryObjects.ElementAt(i), i);

                        value = isArray ? tmp : Activator.CreateInstance(property.PropertyType, new object[] { tmp });
                    }
                    else
                    {
                        string valueAsString = queries.FirstOrDefault();
                        value = valueAsString.ConvertValueToType(property.PropertyType);
                    }

                    if (value == null)
                        continue;

                    property.SetValue(obj, value, null);
                }
                return obj;
            }
            catch (Exception ex)
            {
                throw new SWException($"Error constructing type: '{type.Name}' from parameters. {ex.Message}");

            }

        }



    }
}

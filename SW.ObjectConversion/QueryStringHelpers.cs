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

                                bool isEnumerable = property.PropertyType.GetInterface(nameof(IEnumerable)) != null;
                                if (isEnumerable)
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


        public static object GetFromQueryString(Type type, HttpRequest req)
        {
            try
            {
                var obj = Activator.CreateInstance(type);
                var properties = type.GetProperties();
                foreach (var property in properties)
                {
                    object value = null;
                    Type propType = property.PropertyType;
                    if (propType.IsInterface)
                        throw new SWException($"Type of {property.Name} is an interface. If it's a collection type, consider using List<> or an array.");

                    StringValues queries = req.Query[property.Name];

                    bool isEnumerable = property.PropertyType.GetInterface(nameof(IEnumerable)) != null;

                    if (isEnumerable)
                    {
                        Type nested = propType.GetElementType() ?? propType.GetGenericArguments()[0];
                        Array tmp = Array.CreateInstance(nested, queries.Count);

                        var queryObjects = queries.Select(q => q.ConvertValueToType(nested));
                        for (int i = 0; i < queries.Count; i++)
                            tmp.SetValue(queryObjects.ElementAt(i), i);

                        Type listType = propType.IsInterface ? typeof(List<object>) : propType;
                        bool isArray = propType.IsArray;
                        value = isArray ? tmp : Activator.CreateInstance(listType, new object[] { new object[] { tmp } });
                    }
                    else
                    {
                        value = queries.FirstOrDefault()
                                .ConvertValueToType(propType);
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

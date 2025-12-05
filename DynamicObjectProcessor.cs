using Newtonsoft.Json.Linq;

namespace dynamic-api
{
    public delegate string ProcessString(string valueToProcess);

    public static class DynamicObjectProcessor
    {
        public static dynamic Process(List<string> keys, dynamic body, ProcessString processString)
        {
            if (keys.Count == 0)
                return body;

            var stringToProcess = "";

            foreach (var key in keys)
            {
                var splitKey = key.Split(':');

                // Check for unsupported nesting
                if (splitKey.Length > 2)
                {
                    throw new ArgumentException("Nesting of only one level is supported, e.g. 'parent:parent2:child' is two levels and not supported.");
                }

                try
                {
                    // Handle one level of nesting
                    if (splitKey.Length == 2)
                    {
                        var firstKey = splitKey[0];
                        var isArrayProp = firstKey.Substring(firstKey.Length - 2, 2) == "[]";

                        // Handle Array
                        if (isArrayProp)
                        {
                            var i = 0;
                            var trimmedKey = splitKey[0].TrimEnd(']').TrimEnd('[');
                            var bodyIsArray = trimmedKey.Length is 0;
                            var parentArray = bodyIsArray ? body : body[trimmedKey];

                            if (parentArray is null || !IsDynamicArray(parentArray))
                                continue;

                            foreach (var member in parentArray!)
                            {
                                stringToProcess = member[splitKey[1]]?.ToString();

                                if (stringToProcess is null || !IsDynamicString(stringToProcess))
                                    continue;

                                if (bodyIsArray)
                                {
                                    // set each array member property's processed value ([]:childProp)
                                    body[i][splitKey[1]] = processString(valueToProcess: stringToProcess!);
                                }
                                else
                                {
                                    // set the array member inside the parent property (parentArr[]:childProp)
                                    body[trimmedKey][i][splitKey[1]] = processString(valueToProcess: stringToProcess!);
                                }

                                i++;
                            }
                        }
                        else // Handle Object
                        {
                            var propertyValue = body[splitKey[0]];

                            if (propertyValue is null)
                                continue;

                            stringToProcess = propertyValue[splitKey[1]];

                            if (stringToProcess is null || !IsDynamicString(stringToProcess))
                                continue;

                            body[splitKey[0]][splitKey[1]] = processString(valueToProcess: stringToProcess!);
                        }
                    }
                    else // Handle single level key
                    {
                        var propertyToProcess = body[key];

                        if (propertyToProcess is null || propertyToProcess is not JValue || (propertyToProcess is JValue && ((string)propertyToProcess) is null))
                            continue;

                        if (propertyToProcess is JValue)
                        {
                            propertyToProcess = (string)propertyToProcess;
                        }

                        body[key] = processString(valueToProcess: propertyToProcess!);
                    }
                }
                catch (Exception ex)
                {
                    if (ex is IndexOutOfRangeException || (ex is InvalidOperationException && ex.Message.Contains("Cannot access child value")))
                    {
                        continue;
                    }

                    throw new ApplicationException("There was an issue processing the data. If the parameters are valid, but point to a non-existent member, those parameters will be ignored. If you're receiving this error, the mostly likely cases are that either the data wasn't in the correct format or the provided parameters are invalid. ", ex);
                }
            }

            return body;
        }

        private static dynamic IsDynamicArray(dynamic parentArray)
        {
            var type = parentArray.GetType();
            var result = type == typeof(JArray);
            return result;
        }

        private static dynamic IsDynamicString(dynamic propertyToDecrypt)
        {
            var type = propertyToDecrypt.GetType();
            var result = type == typeof(string);
            return result;
        }
    }
}
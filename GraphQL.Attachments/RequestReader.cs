using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GraphQL.Attachments
{
    public static class RequestReader
    {
        static JsonSerializer serializer = JsonSerializer.CreateDefault();

        public static void ReadGet(HttpRequest request, out string query, out Inputs inputs, out string operationName)
        {
            ReadParams(request.Query.TryGetValue, out query, out inputs, out operationName);
        }

        public static void ReadPost(HttpRequest request, out string query, out Inputs inputs, out IIncomingAttachments attachments, out string operationName, out Dictionary<string, List<string>> map)
        {
            if (request.HasFormContentType)
            {
                ReadForm(request, out query, out inputs,out map, out attachments, out operationName);
                return;
            }

            map = null;
            attachments = new IncomingAttachments();
            ReadBody(request, out query, out inputs, out operationName);
        }

        public class PostBody
        {
            public string OperationName;
            public string Query;
            public JObject Variables;
        }

        static void ReadBody(HttpRequest request, out string query, out Inputs inputs, out string operation)
        {
            using (var streamReader = new StreamReader(request.Body))
            using (var textReader = new JsonTextReader(streamReader))
            {
                var postBody = serializer.Deserialize<PostBody>(textReader);
                query = postBody.Query;
                inputs = postBody.Variables.ToInputs();
                operation = postBody.OperationName;
            }
        }

        static void ReadForm(HttpRequest request, out string query, out Inputs inputs, out Dictionary<string, List<string>> map, out IIncomingAttachments attachments, out string operationName)
        {
            var form = request.Form;
            ReadFormParams(out query, out inputs, out operationName, out map, form);

            attachments = new IncomingAttachments(form.Files.ToDictionary(x => x.FileName, x =>
            {
                return new AttachmentStream(x.FileName, x.OpenReadStream(), x.Length, x.Headers);
            }));
        }

        delegate bool TryGetValue(string key, out StringValues value);
        static void ReadParams(TryGetValue tryGetValue, out string query, out Inputs inputs, out string operationName)
        {
            if (!tryGetValue("query", out var queryValues))
            {
                throw new Exception("Expected to find a form value named 'query'.");
            }

            if (queryValues.Count != 1)
            {
                throw new Exception("Expected 'query' to have a single value.");
            }

            query = queryValues.ToString();

            inputs = GetInputs(tryGetValue);

            operationName = null;
            if (tryGetValue("operation", out var operationValues))
            {
                if (operationValues.Count != 1)
                {
                    throw new Exception("Expected 'operation' to have a single value.");
                }

                operationName = operationValues.ToString();
            }
        }
        static void ReadFormParams(out string query, out Inputs inputs, out string operationName, out Dictionary<string, List<string>> map, IFormCollection form)
        {
            if (!form.TryGetValue("operations", out var operationsValues))
            {
                throw new Exception("Expected to find a form value named 'operations'.");
            }

            if (operationsValues.Count != 1)
            {
                throw new Exception("Expected 'operations' to have a single value.");
            }

            var operations = operationsValues.ToString();

            var jObject = JObject.Parse(operations);

            query = (string) jObject["query"];
            if (string.IsNullOrWhiteSpace(query))
            {
                throw new Exception("Expected 'query' to have a value.");
            }


            operationName = (string) jObject["operationName"];

            if (form.TryGetValue("map", out var mapValues))
            {
                if (mapValues.Count != 1)
                {
                    throw new Exception("Expected 'map' to have a single value.");
                }
                map= JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(mapValues.ToString());
            }
            else
            {
                map = null;
            }

            var variablesToken = jObject["variables"];
            if (variablesToken == null)
            {
                //todo: throw if map not empty?
                inputs = new Inputs();
            }
            else
            {
                var dictionary = (JObject)variablesToken;
                inputs = dictionary.ToInputs();
            }
        }

        static Inputs GetInputs(TryGetValue tryGetValue)
        {
            if (tryGetValue("variables", out var variablesValues))
            {
                if (variablesValues.Count != 1)
                {
                    throw new Exception("Expected 'variables' to have a single value.");
                }

                var json = variablesValues.ToString();
                if (json.Length > 0)
                {
                    var variables = JObject.Parse(json);
                    return variables.ToInputs();
                }
            }

            return new Inputs();
        }
    }
}
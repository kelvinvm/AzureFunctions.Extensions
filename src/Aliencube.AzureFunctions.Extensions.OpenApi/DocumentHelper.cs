﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Aliencube.AzureFunctions.Extensions.OpenApi.Abstractions;
using Aliencube.AzureFunctions.Extensions.OpenApi.Attributes;
using Aliencube.AzureFunctions.Extensions.OpenApi.Extensions;

using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.OpenApi.Models;

namespace Aliencube.AzureFunctions.Extensions.OpenApi
{
    /// <summary>
    /// This represents the helper entity for the <see cref="Document"/> class.
    /// </summary>
    public class DocumentHelper : IDocumentHelper
    {
        /// <inheritdoc />
        public List<MethodInfo> GetHttpTriggerMethods(Assembly assembly)
        {
            var methods = assembly.GetTypes()
                                  .SelectMany(p => p.GetMethods())
                                  .Where(p => p.ExistsCustomAttribute<FunctionNameAttribute>())
                                  .Where(p => !p.ExistsCustomAttribute<OpenApiIgnoreAttribute>())
                                  .Where(p => p.GetParameters().FirstOrDefault(q => q.ExistsCustomAttribute<HttpTriggerAttribute>()) != null)
                                  .ToList();

            return methods;
        }

        /// <inheritdoc />
        public HttpTriggerAttribute GetHttpTriggerAttribute(MethodInfo element)
        {
            var trigger = element.GetHttpTrigger();

            return trigger;
        }

        /// <inheritdoc />
        public FunctionNameAttribute GetFunctionNameAttribute(MethodInfo element)
        {
            var function = element.GetFunctionName();

            return function;
        }

        /// <inheritdoc />
        public string GetHttpEndpoint(FunctionNameAttribute function, HttpTriggerAttribute trigger)
        {
            var endpoint = $"/{(string.IsNullOrWhiteSpace(trigger.Route) ? function.Name : trigger.Route).Trim('/')}";

            return endpoint;
        }

        /// <inheritdoc />
        public OperationType GetHttpVerb(HttpTriggerAttribute trigger)
        {
            var verb = Enum.TryParse<OperationType>(trigger.Methods.First(), true, out OperationType ot)
                           ? ot
                           : throw new InvalidOperationException();

            return verb;
        }

        /// <inheritdoc />
        public OpenApiPathItem GetOpenApiPath(string path, OpenApiPaths paths)
        {
            var item = paths.ContainsKey(path) ? paths[path] : new OpenApiPathItem();

            return item;
        }

        /// <inheritdoc />
        public OpenApiOperation GetOpenApiOperation(MethodInfo element, FunctionNameAttribute function, OperationType verb)
        {
            var op = element.GetOpenApiOperation();
            var operation = new OpenApiOperation()
                                {
                                    OperationId = string.IsNullOrWhiteSpace(op.OperationId) ? $"{function.Name}_{verb}" : op.OperationId,
                                    Tags = op.Tags.Select(p => new OpenApiTag() { Name = p }).ToList()
                                };

            return operation;
        }

        /// <inheritdoc />
        public List<OpenApiParameter> GetOpenApiParameters(MethodInfo element, HttpTriggerAttribute trigger)
        {
            var parameters = element.GetCustomAttributes<OpenApiParameterAttribute>(inherit: false)
                                    .Select(p => p.ToOpenApiParameter())
                                    .ToList();

            if (trigger.AuthLevel != AuthorizationLevel.Anonymous)
            {
                parameters.AddOpenApiParameter<string>("code", @in: ParameterLocation.Query, required: false);
            }

            return parameters;
        }

        /// <inheritdoc />
        public OpenApiRequestBody GetOpenApiRequestBody(MethodInfo element)
        {
            var contents = element.GetCustomAttributes<OpenApiRequestBodyAttribute>(inherit: false)
                                  .ToDictionary(p => p.ContentType, p => p.ToOpenApiMediaType());

            if (contents.Any())
            {
                return new OpenApiRequestBody() { Content = contents };
            }

            return null;
        }

        /// <inheritdoc />
        public OpenApiResponses GetOpenApiResponseBody(MethodInfo element)
        {
            var responses = element.GetCustomAttributes<OpenApiResponseBodyAttribute>(inherit: false)
                                   .ToDictionary(p => ((int)p.StatusCode).ToString(), p => p.ToOpenApiResponse())
                                   .ToOpenApiResponses();

            return responses;
        }

        /// <inheritdoc />
        public Dictionary<string, OpenApiSchema> GetOpenApiSchemas(List<MethodInfo> elements)
        {
            var requests = elements.SelectMany(p => p.GetCustomAttributes<OpenApiRequestBodyAttribute>(inherit: false))
                                   .Select(p => p.BodyType);
            var responses = elements.SelectMany(p => p.GetCustomAttributes<OpenApiResponseBodyAttribute>(inherit: false))
                                    .Select(p => p.BodyType);
            var types = requests.Union(responses)
                                .Distinct();
            var schemas = types.ToDictionary(p => p.Name, p => p.ToOpenApiSchema());

            return schemas;
        }

        /// <inheritdoc />
        public Dictionary<string, OpenApiSecurityScheme> GetOpenApiSecuritySchemes()
        {
            var scheme = new OpenApiSecurityScheme()
                             {
                                 Name = "x-functions-key",
                                 Type = SecuritySchemeType.ApiKey,
                                 In = ParameterLocation.Header
                             };
            var schemes = new Dictionary<string, OpenApiSecurityScheme>()
                              {
                                  { "authKey", scheme }
                              };

            return schemes;
        }
    }
}
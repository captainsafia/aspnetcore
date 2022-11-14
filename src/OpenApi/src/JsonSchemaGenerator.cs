// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization.Metadata;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using System.IO;

namespace Microsoft.AspNetCore.OpenApi;
public class JsonSchemaGenerator
{
    private static readonly Dictionary<Type, Func<OpenApiSchema>> _primitiveTypeToOpenApiSchema =
        new()
        {
            [typeof(bool)] = () => new OpenApiSchema { Type = "boolean" },
            [typeof(byte)] = () => new OpenApiSchema { Type = "string", Format = "byte" },
            [typeof(int)] = () => new OpenApiSchema { Type = "integer", Format = "int32" },
            [typeof(uint)] = () => new OpenApiSchema { Type = "integer", Format = "int32" },
            [typeof(ushort)] = () => new OpenApiSchema { Type = "integer", Format = "int32" },
            [typeof(long)] = () => new OpenApiSchema { Type = "integer", Format = "int64" },
            [typeof(ulong)] = () => new OpenApiSchema { Type = "integer", Format = "int64" },
            [typeof(float)] = () => new OpenApiSchema { Type = "number", Format = "float" },
            [typeof(double)] = () => new OpenApiSchema { Type = "number", Format = "double" },
            [typeof(decimal)] = () => new OpenApiSchema { Type = "number", Format = "double" },
            [typeof(DateTime)] = () => new OpenApiSchema { Type = "string", Format = "date-time" },
            [typeof(DateTimeOffset)] = () => new OpenApiSchema { Type = "string", Format = "date-time" },
            [typeof(Guid)] = () => new OpenApiSchema { Type = "string", Format = "uuid" },
            [typeof(char)] = () => new OpenApiSchema { Type = "string" },
            [typeof(bool?)] = () => new OpenApiSchema { Type = "boolean", Nullable = true },
            [typeof(byte?)] = () => new OpenApiSchema { Type = "string", Format = "byte", Nullable = true },
            [typeof(int?)] = () => new OpenApiSchema { Type = "integer", Format = "int32", Nullable = true },
            [typeof(uint?)] = () => new OpenApiSchema { Type = "integer", Format = "int32", Nullable = true },
            [typeof(ushort?)] = () => new OpenApiSchema { Type = "integer", Format = "int32", Nullable = true },
            [typeof(long?)] = () => new OpenApiSchema { Type = "integer", Format = "int64", Nullable = true },
            [typeof(ulong?)] = () => new OpenApiSchema { Type = "integer", Format = "int64", Nullable = true },
            [typeof(float?)] = () => new OpenApiSchema { Type = "number", Format = "float", Nullable = true },
            [typeof(double?)] = () => new OpenApiSchema { Type = "number", Format = "double", Nullable = true },
            [typeof(decimal?)] = () => new OpenApiSchema { Type = "number", Format = "double", Nullable = true },
            [typeof(DateTime?)] = () => new OpenApiSchema { Type = "string", Format = "date-time", Nullable = true },
            [typeof(DateTimeOffset?)] = () =>
                new OpenApiSchema { Type = "string", Format = "date-time", Nullable = true },
            [typeof(Guid?)] = () => new OpenApiSchema { Type = "string", Format = "uuid", Nullable = true },
            [typeof(char?)] = () => new OpenApiSchema { Type = "string", Nullable = true },
            // Uri is treated as simple string.
            [typeof(Uri)] = () => new OpenApiSchema { Type = "string" },
            [typeof(string)] = () => new OpenApiSchema { Type = "string" },
            [typeof(object)] = () => new OpenApiSchema { Type = "object" }
        };

    private OpenApiDocument _document;
    private JsonOptions _options;

    public JsonSchemaGenerator(OpenApiDocument document, IOptions<JsonOptions> jsonOptions)
    {
        _document = document;
        _options = jsonOptions.Value;
    }

    public OpenApiSchema GetSchemaFromType(Type type)
    {
        // Make sure that we are passing JsonSerializationOptions
        var typeInfoResolver = _options.SerializerOptions.TypeInfoResolver;
        var jsonType = typeInfoResolver.GetTypeInfo(type, _options.SerializerOptions);
        if (_document?.Components?.Schemas.TryGetValue(jsonType.Type.Name, out var componentSchema) == true)
        {
            return new OpenApiSchema { Reference = new OpenApiReference { Type = ReferenceType.Schema, Id = jsonType.Type.Name } };
        }
        var schema = new OpenApiSchema();
        if (jsonType.Kind == JsonTypeInfoKind.None)
        {
            if (type.IsEnum)
            {
                schema = _primitiveTypeToOpenApiSchema.TryGetValue(type.GetEnumUnderlyingType(), out var enumResult)
                    ? enumResult()
                    : new OpenApiSchema { Type = "string" };
                foreach (var value in Enum.GetValues(type))
                {
                    schema.Enum.Add(new OpenApiInteger((int)value));
                }
            }
            else
            {
                schema = _primitiveTypeToOpenApiSchema.TryGetValue(type, out var result)
                    ? result()
                    : new OpenApiSchema { Type = "string" };
            }

        }
        if (jsonType.Kind == JsonTypeInfoKind.Dictionary)
        {
            schema.Type = "object";
            schema.AdditionalPropertiesAllowed = true;
            var genericTypeArgs = jsonType.Type.GetGenericArguments();
            Type? valueType = null;
            if (genericTypeArgs.Length == 2)
            {
                valueType = jsonType.Type.GetGenericArguments().Last();
            }
            schema.AdditionalProperties = _primitiveTypeToOpenApiSchema.TryGetValue(valueType, out var result)
                ? result()
                : new OpenApiSchema { };
        }
        if (jsonType.Kind == JsonTypeInfoKind.Enumerable)
        {
            schema.Type = "array";
            var elementType = jsonType.Type.GetGenericArguments().Last();
            schema.Items = GetSchemaFromType(elementType);
        }
        if (jsonType.Kind == JsonTypeInfoKind.Object)
        {
            schema.Type = "object";
            if (jsonType.PolymorphismOptions is not null)
            {
                var derivedTypes = jsonType.PolymorphismOptions.DerivedTypes;
                foreach (var derivedType in derivedTypes)
                {
                    if (_document?.Components?.Schemas.TryGetValue(derivedType.DerivedType.Name, out var cSchema) == true)
                    {
                        cSchema.AllOf = new List<OpenApiSchema>()
                        {
                            new OpenApiSchema { Reference = new OpenApiReference { Type = ReferenceType.Schema, Id = jsonType.Type.Name } }
                        };
                        foreach (var property in jsonType.Properties)
                        {
                            cSchema.Properties.Remove(property.Name);
                        }
                    }
                    else
                    {
                        _document.Components ??= new OpenApiComponents();
                        var _ = GetSchemaFromType(derivedType.DerivedType);
                        if (_document?.Components?.Schemas.TryGetValue(derivedType.DerivedType.Name, out var fSchema) == true)
                        {
                            fSchema.AllOf = new List<OpenApiSchema>()
                        {
                            new OpenApiSchema { Reference = new OpenApiReference { Type = ReferenceType.Schema, Id = jsonType.Type.Name } }
                        };
                            foreach (var property in jsonType.Properties)
                            {
                                fSchema.Properties.Remove(property.Name);
                            }
                            _document.Components.Schemas[derivedType.DerivedType.Name] = fSchema;
                        }
                    }
                }
            }
            foreach (var property in jsonType.Properties)
            {
                var innerSchema = GetSchemaFromType(property.PropertyType);
                var defaultValueAttribute = property.AttributeProvider.GetCustomAttributes(true).OfType<DefaultValueAttribute>().FirstOrDefault();
                if (defaultValueAttribute != null)
                {
                    innerSchema.Default = OpenApiAnyFactory.CreateFromJson(JsonSerializer.Serialize(defaultValueAttribute.Value));
                }
                innerSchema.ReadOnly = property.Set is null;
                innerSchema.WriteOnly = property.Get is null;
                schema.Properties.Add(property.Name, innerSchema);
            }
            _document.Components ??= new OpenApiComponents();
            _document.Components.Schemas.Add(jsonType.Type.Name, schema);
            if (jsonType.PolymorphismOptions is not null)
            {
                var oneOf = new List<OpenApiSchema>();
                oneOf.Add(new OpenApiSchema { Reference = new OpenApiReference { Type = ReferenceType.Schema, Id = jsonType.Type.Name } });
                foreach (var derivedType in jsonType.PolymorphismOptions.DerivedTypes)
                {
                    oneOf.Add(new OpenApiSchema { Reference = new OpenApiReference { Type = ReferenceType.Schema, Id = derivedType.DerivedType.Name } });
                }
                return new OpenApiSchema { OneOf = oneOf };
            }
            return new OpenApiSchema { Reference = new OpenApiReference { Type = ReferenceType.Schema, Id = jsonType.Type.Name } };
        }
        return schema;
    }
}

public static class OpenApiAnyFactory
{
    public static IOpenApiAny CreateFromJson(string json)
    {
        try
        {
            var jsonElement = JsonSerializer.Deserialize<JsonElement>(json);

            return CreateFromJsonElement(jsonElement);
        }
        catch { }

        return null;
    }

    private static IOpenApiAny CreateOpenApiArray(JsonElement jsonElement)
    {
        var openApiArray = new OpenApiArray();

        foreach (var item in jsonElement.EnumerateArray())
        {
            openApiArray.Add(CreateFromJsonElement(item));
        }

        return openApiArray;
    }

    private static IOpenApiAny CreateOpenApiObject(JsonElement jsonElement)
    {
        var openApiObject = new OpenApiObject();

        foreach (var property in jsonElement.EnumerateObject())
        {
            openApiObject.Add(property.Name, CreateFromJsonElement(property.Value));
        }

        return openApiObject;
    }

    private static IOpenApiAny CreateFromJsonElement(JsonElement jsonElement)
    {
        if (jsonElement.ValueKind == JsonValueKind.Null)
        {
            return new OpenApiNull();
        }

        if (jsonElement.ValueKind == JsonValueKind.True || jsonElement.ValueKind == JsonValueKind.False)
        {
            return new OpenApiBoolean(jsonElement.GetBoolean());
        }

        if (jsonElement.ValueKind == JsonValueKind.Number)
        {
            if (jsonElement.TryGetInt32(out int intValue))
            {
                return new OpenApiInteger(intValue);
            }

            if (jsonElement.TryGetInt64(out long longValue))
            {
                return new OpenApiLong(longValue);
            }

            if (jsonElement.TryGetSingle(out float floatValue) && !float.IsInfinity(floatValue))
            {
                return new OpenApiFloat(floatValue);
            }

            if (jsonElement.TryGetDouble(out double doubleValue))
            {
                return new OpenApiDouble(doubleValue);
            }
        }

        if (jsonElement.ValueKind == JsonValueKind.String)
        {
            return new OpenApiString(jsonElement.ToString());
        }

        if (jsonElement.ValueKind == JsonValueKind.Array)
        {
            return CreateOpenApiArray(jsonElement);
        }

        if (jsonElement.ValueKind == JsonValueKind.Object)
        {
            return CreateOpenApiObject(jsonElement);
        }

        throw new System.ArgumentException($"Unsupported value kind {jsonElement.ValueKind}");
    }
}

using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace WhatAreWeEating.Api.Swagger;

/// <summary>
/// Solo documenta para Swagger el formulario de POST /documentos (campo "archivo", multipart/form-data) para que
/// aparezca el selector de archivo. No cambia el comportamiento: el endpoint lee el formulario a mano y responde
/// 400 con mensaje si falta el archivo, venga lo que venga en el cuerpo (RD-07).
/// </summary>
public class SubidaDocumentoOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (operation.OperationId != "SubirDocumento")
        {
            return;
        }

        operation.RequestBody = new OpenApiRequestBody
        {
            Required = true,
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["multipart/form-data"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.Object,
                        Properties = new Dictionary<string, IOpenApiSchema>
                        {
                            ["archivo"] = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" }
                        }
                    }
                }
            }
        };
    }
}

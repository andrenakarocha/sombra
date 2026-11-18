using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Sombra.Api.Composicao;

/// <summary>O cabeçalho do documento OpenAPI e o esquema de segurança Bearer (JWT do login).</summary>
public sealed class DocumentoDoSombra : IOpenApiDocumentTransformer
{
    /// <summary>O nome do esquema de segurança.</summary>
    public const string Bearer = "Bearer";

    /// <inheritdoc/>
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        document.Info = new OpenApiInfo
        {
            Title = "Sombra · Core API",
            Version = "v1",
            Description = "Alerta de calor extremo para pessoas idosas que moram sozinhas: casas, moradores, rede de cuidado, "
                + "Sentinelas, leituras, alertas e check-ins. Entre por POST /api/v1/auth/login e use o token no botão de autenticação. "
                + "Erros em ProblemDetails (RFC 9457), PII sempre mascarada, dados de demonstração fictícios.",
        };
        document.Servers?.Clear();
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[Bearer] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "O token devolvido por POST /api/v1/auth/login.",
        };
        return Task.CompletedTask;
    }
}

/// <summary>
/// Em toda operação protegida: o requisito do Bearer e as respostas 401 e 403. Em todas: a 429 do limite de requisições.
/// </summary>
public sealed class RespostasDeSeguranca : IOpenApiOperationTransformer
{
    /// <inheritdoc/>
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);
        IList<object> metadados = context.Description.ActionDescriptor.EndpointMetadata;
        operation.Responses ??= [];
        if (metadados.OfType<IAuthorizeData>().Any() && !metadados.OfType<IAllowAnonymous>().Any())
        {
            operation.Security ??= [];
            operation.Security.Add(new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(DocumentoDoSombra.Bearer, context.Document)] = [] });
            operation.Responses.TryAdd(StatusCodes.Status401Unauthorized.ToString(System.Globalization.CultureInfo.InvariantCulture), new OpenApiResponse { Description = "Sem token, ou token inválido ou vencido." });
            operation.Responses.TryAdd(StatusCodes.Status403Forbidden.ToString(System.Globalization.CultureInfo.InvariantCulture), new OpenApiResponse { Description = "O papel não permite, ou a casa não é do Cuidador." });
        }

        operation.Responses.TryAdd(StatusCodes.Status429TooManyRequests.ToString(System.Globalization.CultureInfo.InvariantCulture), new OpenApiResponse { Description = "Limite de requisições por minuto atingido." });
        return Task.CompletedTask;
    }
}

/// <summary>Documenta o cabeçalho <c>Idempotency-Key</c> nas escritas marcadas com <see cref="IdempotenteAttribute"/>.</summary>
public sealed class CabecalhoDeIdempotencia : IOpenApiOperationTransformer
{
    /// <inheritdoc/>
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);
        if (context.Description.ActionDescriptor.EndpointMetadata.OfType<IdempotenteAttribute>().Any())
        {
            operation.Parameters ??= [];
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = FiltroDeIdempotencia.Cabecalho,
                In = ParameterLocation.Header,
                Required = false,
                Description = "Identifica a operação da fila offline do app (o operationId). A mesma chave, pelo mesmo usuário e na mesma rota, "
                    + "devolve a mesma resposta por 24 horas, sem repetir o efeito (com o cabeçalho Idempotency-Replayed: true). "
                    + "Usada em outra rota: 422 com regra IDEMPOTENCIA.",
                Schema = new OpenApiSchema { Type = JsonSchemaType.String, MaxLength = 100, Pattern = "^[A-Za-z0-9._:-]{1,100}$" },
            });
        }

        return Task.CompletedTask;
    }
}


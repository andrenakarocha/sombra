# Sombra · Core API. Compila no SDK e roda no runtime ASP.NET Core em Alpine, sem root (docs/PLANO.md §8.2).
FROM mcr.microsoft.com/dotnet/sdk:10.0.404 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props .editorconfig ./
COPY sources/ sources/
RUN dotnet publish sources/Sombra.Api/Sombra.Api.csproj -c Release -o /app --nologo -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.15-alpine3.24
ARG COMMIT=dev
ARG CONSTRUIDO_EM=
ENV ASPNETCORE_HTTP_PORTS=5080 \
    Versao__Commit=${COMMIT} \
    Versao__ConstruidoEm=${CONSTRUIDO_EM}
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 5080
ENTRYPOINT ["dotnet", "Sombra.Api.dll"]

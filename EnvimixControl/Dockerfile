FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine-aot AS build
WORKDIR /src

COPY EnvimixControl.csproj ./
RUN dotnet restore

COPY . ./
RUN dotnet publish --no-restore --self-contained -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine
WORKDIR /app

COPY --from=build /app/publish/EnvimixControl ./EnvimixControl
USER $APP_UID
ENTRYPOINT ["./EnvimixControl"]

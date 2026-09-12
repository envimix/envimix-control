FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine-aot AS build
ARG TARGETARCH
WORKDIR /src

COPY EnvimixControl.csproj ./
RUN dotnet restore -a $TARGETARCH

COPY . ./
RUN dotnet publish --no-restore -a $TARGETARCH --self-contained -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-alpine
WORKDIR /app

COPY --from=build /app/publish/EnvimixControl ./EnvimixControl
USER $APP_UID
ENTRYPOINT ["./EnvimixControl"]

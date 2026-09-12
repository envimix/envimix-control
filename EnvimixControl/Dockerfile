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

# Allow the application user to create and manage UserData files.
RUN mkdir -p /app/UserData && \
	chown -R $APP_UID:$APP_UID /app/UserData && \
	chmod -R 775 /app/UserData

USER $APP_UID
ENTRYPOINT ["./EnvimixControl"]

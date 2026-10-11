# Multi-arch (amd64 / arm64 Raspberry Pi):  docker buildx build --platform linux/arm64,linux/amd64 -t vigil-atlas .
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG TARGETARCH
WORKDIR /src
COPY VigilAtlas/ VigilAtlas/
RUN dotnet publish VigilAtlas/VigilAtlas.csproj -c Release -a ${TARGETARCH/amd64/x64} -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "VigilAtlas.dll"]

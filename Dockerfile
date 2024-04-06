FROM mcr.microsoft.com/dotnet/sdk:8.0-jammy AS build
WORKDIR /app
COPY LeetifyWebhook.csproj .
RUN dotnet restore --runtime linux-x64 LeetifyWebhook.csproj

COPY . .
RUN apt update && apt-get install -y clang zlib1g-dev
RUN dotnet publish -c Release -r linux-x64 -o out LeetifyWebhook.csproj

FROM mcr.microsoft.com/dotnet/runtime-deps:8.0-jammy-chiseled AS runtime
WORKDIR /app
COPY --from=build /app/out/LeetifyWebhook /app/
ENV ASPNETCORE_URLS="https://+;http://+"
EXPOSE 443
EXPOSE 80
ENTRYPOINT ["/app/LeetifyWebhook"]  
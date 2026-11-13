FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra
WORKDIR /app
COPY artifacts/PrefillJob/ ./
ENTRYPOINT ["dotnet", "PrefillJob.dll"]

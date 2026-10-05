FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY artifacts/PrefillJob/ ./
ENTRYPOINT ["dotnet", "PrefillJob.dll"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra
WORKDIR /app
COPY artifacts/ProfanityService/ ./
EXPOSE 8080
ENTRYPOINT ["dotnet", "ProfanityService.dll"]

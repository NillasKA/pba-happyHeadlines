FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY artifacts/PublisherService/ ./
EXPOSE 8080
ENTRYPOINT ["dotnet", "PublisherService.dll"]

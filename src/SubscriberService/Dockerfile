FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra
WORKDIR /app
COPY artifacts/SubscriberService/ ./
EXPOSE 8080
ENTRYPOINT ["dotnet", "SubscriberService.dll"]

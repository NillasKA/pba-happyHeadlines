FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra
WORKDIR /app
COPY artifacts/ArticleService/ ./
EXPOSE 8080
ENTRYPOINT ["dotnet", "ArticleService.dll"]

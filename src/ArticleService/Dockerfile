FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY artifacts/ArticleService/ ./
EXPOSE 8080
ENTRYPOINT ["dotnet", "ArticleService.dll"]

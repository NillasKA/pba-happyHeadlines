FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY artifacts/NewsletterService/ ./
EXPOSE 8080
ENTRYPOINT ["dotnet", "NewsletterService.dll"]

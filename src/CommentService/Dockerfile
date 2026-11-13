FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra
WORKDIR /app
COPY artifacts/CommentService/ ./
EXPOSE 8080
ENTRYPOINT ["dotnet", "CommentService.dll"]

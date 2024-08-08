FROM mcr.microsoft.com/dotnet/aspnet:6.0
RUN apt-get update && apt-get install curl -y
WORKDIR /app
COPY buildfolder/ ./
EXPOSE 80
ENTRYPOINT ["dotnet", "SynapseDynamicAPI.dll"]

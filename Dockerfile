# استخدم SDK عشان تعمل build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# انسخ ملفات الحل
COPY ["fabrics/fabrics.csproj", "fabrics/"]
RUN dotnet restore "fabrics/fabrics.csproj"

COPY . .
WORKDIR "/src/fabrics"
RUN dotnet publish "fabrics.csproj" -c Release -o /app/publish

# استخدم runtime عشان تشغل المشروع
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "fabrics.dll"]

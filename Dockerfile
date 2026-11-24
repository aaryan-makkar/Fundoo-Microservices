FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

ARG PROJECT
ARG ASSEMBLY
RUN test -n "$PROJECT" && test -n "$ASSEMBLY"
COPY . .
RUN dotnet publish "$PROJECT" -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ARG ASSEMBLY
COPY --from=build /out .
ENV APP_ASSEMBLY=${ASSEMBLY}
ENTRYPOINT ["sh", "-c", "dotnet \"$APP_ASSEMBLY.dll\""]

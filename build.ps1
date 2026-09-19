# SnowRunner-Tool Build Script
# .NET 8: build app and run unit tests

dotnet test SnowRunner-Tool.sln -c Debug
exit $LASTEXITCODE

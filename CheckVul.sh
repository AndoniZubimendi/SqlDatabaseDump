#!/bin/sh
echo Checking vulnerabilities...
dotnet restore
dotnet list package --vulnerable --include-transitive

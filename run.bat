@echo off
title ALKOR - Rotating Leader Konsensus Simulasyonu
echo.
echo  ALKOR Simulasyonu derleniyor...
echo.
cd /d "%~dp0"
dotnet run --configuration Release

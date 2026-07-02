@echo off
REM This batch is used in my workstation to collect plugins and profiles
REM from several Cadmus solutions.
echo UPDATE PLUGINS
set target=.\bin\Debug\net7.0\plugins

md %target%
del %target%\*.* /q
REM Pura
xcopy ..\..\Pura\CadmusPuraApi\CadmusPuraApi\wwwroot\seed-profile.json %target%\Cadmus.Pura.Services\ /y
xcopy ..\..\Tgr\CadmusTgr\Cadmus.Seed.Tgr.Parts\bin\Debug\net7.0\*.* %target%\Cadmus.Pura.Services\ /y
xcopy ..\..\Pura\CadmusPura\Cadmus.Pura.Services\bin\Debug\net7.0\*.* %target%\Cadmus.Pura.Services\ /y
pause
REM Renovella
xcopy ..\..\Renovella\CadmusRenovellaApi\CadmusRenovellaApi\wwwroot\seed-profile.json %target%\Cadmus.Renovella.Services\ /y
xcopy ..\..\CadmusBricks\Cadmus.Refs.Bricks\bin\Debug\net7.0\*.* %target%\Cadmus.Renovella.Services\ /y
xcopy ..\..\Renovella\CadmusRenovella\Cadmus.Renovella.Services\bin\Debug\net7.0\*.* %target%\Cadmus.Renovella.Services\ /y
pause
REM Tgr
xcopy ..\..\Tgr\CadmusTgrApi\CadmusTgrApi\wwwroot\seed-profile.json %target%\Cadmus.Tgr.Services\ /y
xcopy ..\..\CadmusBricks\Cadmus.Refs.Bricks\bin\Debug\net7.0\*.* %target%\Cadmus.Tgr.Services\ /y
xcopy ..\..\Tgr\CadmusTgr\Cadmus.Tgr.Services\bin\Debug\net7.0\*.* %target%\Cadmus.Tgr.Services\ /y
REM Sidon
xcopy ..\..\Sidon\CadmusSidonApi\CadmusSidonApi\wwwroot\seed-profile.json %target%\Cadmus.Sidon.Services\ /y
xcopy ..\..\CadmusBricks\Cadmus.Refs.Bricks\bin\Debug\net7.0\*.* %target%\Cadmus.Sidon.Services\ /y
pause

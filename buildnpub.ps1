# BUILD AND PUBLISH SCRIPT FOR .NET PROJECTS
# Packages are pushed incrementally after each pack to resolve inter-project dependencies.
# - to pack all packages:
#   .\buildnpub.ps1 -Pack
# - to pack and push locally:
#   .\buildnpub.ps1 -Pack -PushLocal
# - to pack and push to NuGet.org:
#   .\buildnpub.ps1 -Pack -PushNuGet
# - to pack and push to both:
#   .\buildnpub.ps1 -Pack -PushLocal -PushNuGet

param(
    [switch]$Pack,
    [switch]$PushLocal,
    [switch]$PushNuGet,
    [string]$LocalFeed = "C:\Projects\_NuGet",
    [string]$NuGetExe = "C:\Exe\nuget.exe"
)

Write-Host "Build & Publish Script" -ForegroundColor Cyan

# 1. Define projects in dependency order (no dependencies first, then dependents)
# Order determined by analyzing PackageReference elements in Release configuration
$projectOrder = @(
    "Cadmus.Core\Cadmus.Core.csproj",
    "Cadmus.Seed\Cadmus.Seed.csproj",
    "Cadmus.Cli.Core\Cadmus.Cli.Core.csproj",
    "Cadmus.Refs.Bricks\Cadmus.Refs.Bricks.csproj",
    "Cadmus.Philology.Tools\Cadmus.Philology.Tools.csproj",
    "Cadmus.Philology.Parts\Cadmus.Philology.Parts.csproj",
    "Cadmus.Mat.Bricks\Cadmus.Mat.Bricks.csproj",
    "Cadmus.General.Parts\Cadmus.General.Parts.csproj",
    "Cadmus.Mongo\Cadmus.Mongo.csproj",
    "Proteus.Rendering\Proteus.Rendering.csproj",
    "Cadmus.Export\Cadmus.Export.csproj",
    "Cadmus.Export.ML\Cadmus.Export.ML.csproj",
    "Cadmus.Export.Rdf\Cadmus.Export.Rdf.csproj",
    "Cadmus.Graph\Cadmus.Graph.csproj",
    "Cadmus.Graph.Ef\Cadmus.Graph.Ef.csproj",
    "Cadmus.Graph.Ef.PgSql\Cadmus.Graph.Ef.PgSql.csproj",
    "Cadmus.Graph.Extras\Cadmus.Graph.Extras.csproj",
    "Cadmus.Import\Cadmus.Import.csproj",
    "Cadmus.Import.Excel\Cadmus.Import.Excel.csproj",
    "Cadmus.Index\Cadmus.Index.csproj",
    "Cadmus.Index.Sql\Cadmus.Index.Sql.csproj",
    "Cadmus.Index.Ef\Cadmus.Index.Ef.csproj",
    "Cadmus.Index.PgSql\Cadmus.Index.PgSql.csproj",
    "Cadmus.Index.Ef.PgSql\Cadmus.Index.Ef.PgSql.csproj",
    "Cadmus.Seed.General.Parts\Cadmus.Seed.General.Parts.csproj",
    "Cadmus.Seed.Philology.Parts\Cadmus.Seed.Philology.Parts.csproj",
    "Cadmus.Api.Services\Cadmus.Api.Services.csproj",
    "Cadmus.Api.Config\Cadmus.Api.Config.csproj",
    "Cadmus.Api.Controllers.Export\Cadmus.Api.Controllers.Export.csproj",
    "Cadmus.Api.Models\Cadmus.Api.Models.csproj",
    "Cadmus.Api.Controllers.Import\Cadmus.Api.Controllers.Import.csproj",
    "Cadmus.Api.Controllers\Cadmus.Api.Controllers.csproj",
    "Cadmus.Import.Proteus\Cadmus.Import.Proteus.csproj"
)

if ($Pack) {
    Write-Host "`n=== PACKING PROJECTS IN DEPENDENCY ORDER ===" -ForegroundColor Yellow

    foreach ($projPath in $projectOrder) {
        $fullPath = Join-Path $PSScriptRoot $projPath

        if (Test-Path $fullPath) {
            $projDir = Split-Path $fullPath
            $releaseDir = Join-Path $projDir "bin\Release"

            # Remove packages left over from previous runs so old versions
            # don't accumulate and get re-pushed alongside the new one.
            if (Test-Path $releaseDir) {
                Get-ChildItem $releaseDir -Filter *.nupkg -ErrorAction SilentlyContinue | Remove-Item -Force
                Get-ChildItem $releaseDir -Filter *.snupkg -ErrorAction SilentlyContinue | Remove-Item -Force
            }

            Write-Host "`nPacking $projPath" -ForegroundColor Green
            dotnet pack $fullPath -c Release

            if ($LASTEXITCODE -ne 0) {
                Write-Host "ERROR: Failed to pack $projPath" -ForegroundColor Red
                exit 1
            }

            $nupkgs = Get-ChildItem $releaseDir -Filter *.nupkg -ErrorAction SilentlyContinue

            # If pushing locally, add the package immediately after packing
            if ($PushLocal) {
                foreach ($pkg in $nupkgs) {
                    Write-Host "  -> Adding $($pkg.Name) to local feed" -ForegroundColor Cyan
                    & $NuGetExe add $pkg.FullName -Source $LocalFeed
                }
            }

            # If pushing to NuGet.org, push immediately after packing
            if ($PushNuGet) {
                foreach ($pkg in $nupkgs) {
                    Write-Host "  -> Pushing $($pkg.Name) to NuGet.org" -ForegroundColor Cyan
                    & $NuGetExe push $pkg.FullName -Source https://api.nuget.org/v3/index.json -SkipDuplicate
                }
            }
        } else {
            Write-Host "WARNING: Project not found: $fullPath" -ForegroundColor Yellow
        }
    }
}

# 2. Push to local feed (already done incrementally above if -PushLocal was specified with -Pack)
if ($PushLocal -and -not $Pack) {
    Write-Host "`n=== PUSHING TO LOCAL FEED ===" -ForegroundColor Yellow
    Write-Host "Note: Packages are already added incrementally during packing." -ForegroundColor Cyan
    Write-Host "This section only runs if you use -PushLocal without -Pack." -ForegroundColor Cyan
}

# 3. Push to NuGet.org (only runs if -PushNuGet without -Pack)
if ($PushNuGet -and -not $Pack) {
    Write-Host "`n=== PUSHING TO NUGET.ORG ===" -ForegroundColor Yellow
    Write-Host "Note: Packages are pushed incrementally during packing when using -Pack -PushNuGet." -ForegroundColor Cyan

    foreach ($projPath in $projectOrder) {
        $fullPath = Join-Path $PSScriptRoot $projPath

        if (Test-Path $fullPath) {
            $projDir = Split-Path $fullPath
            $nupkgs = Get-ChildItem "$projDir\bin\Release" -Filter *.nupkg -ErrorAction SilentlyContinue

            foreach ($pkg in $nupkgs) {
                Write-Host "Pushing $($pkg.Name) to NuGet.org" -ForegroundColor Green
                & $NuGetExe push $pkg.FullName -Source https://api.nuget.org/v3/index.json -SkipDuplicate
            }
        }
    }
}

Write-Host "`nDone." -ForegroundColor Cyan
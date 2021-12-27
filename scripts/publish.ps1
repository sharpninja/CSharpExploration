param(
    [string]$Token=$null,
    [string]$WorkingFolder=".",
    [switch]$WhatIf=$false
)

$exitCode = 0;

function Test-ExitCode {
    param(
        [int]$LEC,
        [string]$ErrorDescription)

    $exitCode = $LEC

    if($exitCode -ne 0) {
        throw "${exitCode}: ${ErrorDescription}"
    } else {
        # "`$exitCode: $exitCode"
    }
}

Push-Location

try {
    Set-Location $WorkingFolder -Verbose:$Verbose

    if((-not $token) -or ($token.Length -eq 0)) {
        throw "No Nuget Token was supplied."
    }

    $config = Get-ChildItem nuget.config -ErrorAction Stop

    if (-not $config) {
        throw "Could not locate nuget.config in the root of the project."
    }

    $ConfigFile = $config.FullName

    $project = Get-ChildItem -Path ./src *.csproj -Recurse -ErrorAction Stop

    if($project) {
        $csproj = $project.FullName
        "Building [$csproj]..."

        & dotnet build $csproj -c Release --no-restore #--nologo -v quiet

        Test-ExitCode $LASTEXITCODE "Failed to build [${project.FullName}]."

        "Getting Packages..."

        $packages = Get-ChildItem *.symbols.nupkg -Path ./src -Recurse -ErrorAction Stop -Verbose `
            | Sort-Object ModifiedDate;

        if((-not $packages) -or ($packages.Length -eq 0)) {
            throw "No packages were built for [$csproj]."
        }

        "Packages to publish..."

        $packages `
            | Sort-Object Directory, Name `
            | Format-Table Name, Directory

        $packages `
            | ForEach-Object -Verbose -Process {
                $package = $_

                try {
                    $Name = $package.Name

                    Set-Location $package.Directory

                    Copy-Item $ConfigFile . -ErrorAction Stop

                    if(!$WhatIf) {
                        "& dotnet nuget push $Name --source `"nuget`" -k `"`${token}`" --skip-duplicate"
                        & dotnet nuget push $Name --source "nuget" -k "${token}" --skip-duplicate
                    } else {
                        "WhatIf: & dotnet nuget push $Name -k `"`${token}`"  --config $ConfigFile # --skip-duplicate"
                    }

                    Test-ExitCode $LASTEXITCODE "Failed to push [${package.Name}]."
                }
                catch {
                    throw $_
                }

                "Publish Finished Successfully."
        }
    }
    else {
        throw "No csproj file in $PWD (recursive)."
    }
}
catch {
    if($exitCode -eq 0) { $exitCode = $LASTEXITCODE }
    $err = $_
    $err
    exit $exitCode
}
finally {
    Pop-Location
}

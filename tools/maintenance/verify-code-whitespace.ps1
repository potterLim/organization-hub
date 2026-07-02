$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$CODE_EXTENSIONS = @(
    ".cs",
    ".csproj",
    ".css",
    ".editorconfig",
    ".gitattributes",
    ".gitignore",
    ".json",
    ".razor",
    ".sln",
    ".slnx",
    ".ts",
    ".yml",
    ".yaml"
)

function Test-CodePath
{
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    [string] $extension = [System.IO.Path]::GetExtension($Path)
    return $CODE_EXTENSIONS.Contains($extension)
}

function Get-ChangedCodePaths
{
    param(
        [string[]] $GitArguments
    )

    [string[]] $changedPaths = & git diff @GitArguments --name-only --diff-filter=ACMRT
    if ($LASTEXITCODE -ne 0)
    {
        exit $LASTEXITCODE
    }

    [System.Collections.Generic.List[string]] $changedCodePaths = New-Object "System.Collections.Generic.List[string]"
    foreach ($changedPath in $changedPaths)
    {
        if (Test-CodePath -Path $changedPath)
        {
            $changedCodePaths.Add($changedPath)
        }
    }

    return $changedCodePaths.ToArray()
}

function Invoke-CodeWhitespaceCheck
{
    param(
        [string[]] $GitArguments
    )

    [string[]] $changedCodePaths = @(Get-ChangedCodePaths -GitArguments $GitArguments)
    if ($changedCodePaths.Length -eq 0)
    {
        return 0
    }

    & git diff @GitArguments --check -- $changedCodePaths
    return $LASTEXITCODE
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
Push-Location $repoRoot
try
{
    [int] $unstagedExitCode = Invoke-CodeWhitespaceCheck -GitArguments @()
    if ($unstagedExitCode -ne 0)
    {
        exit $unstagedExitCode
    }

    [int] $stagedExitCode = Invoke-CodeWhitespaceCheck -GitArguments @("--cached")
    exit $stagedExitCode
}
finally
{
    Pop-Location
}

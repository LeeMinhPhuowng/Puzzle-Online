param(
    [int]$Port = 7777,
    [string]$DataDirectory = "server-data"
)

$ErrorActionPreference = "Stop"
$serverRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourceRoot = Join-Path $serverRoot "src\main\java"
$buildRoot = Join-Path $serverRoot "build\classes"
$libDir = Join-Path $serverRoot "lib"

New-Item -ItemType Directory -Force -Path $buildRoot | Out-Null

$libJars = Get-ChildItem -LiteralPath $libDir -Filter "*.jar" -ErrorAction SilentlyContinue | ForEach-Object { $_.FullName }
$classpath = if ($libJars) { "$buildRoot;" + ($libJars -join ";") } else { $buildRoot }

$sources = Get-ChildItem -LiteralPath $sourceRoot -Recurse -Filter "*.java" | ForEach-Object { $_.FullName }
if ($sources.Count -eq 0) { throw "No Java source files found in $sourceRoot" }

Write-Host "Compiling Puzzle Online server..."
if ($libJars) {
    & javac -encoding UTF-8 -cp ($libJars -join ";") -d $buildRoot $sources
} else {
    & javac -encoding UTF-8 -d $buildRoot $sources
}
if ($LASTEXITCODE -ne 0) { throw "javac failed with exit code $LASTEXITCODE" }

$resolvedData = if ([System.IO.Path]::IsPathRooted($DataDirectory)) { $DataDirectory } else { Join-Path $serverRoot $DataDirectory }
Write-Host "Starting server on port $Port with MySQL backend..."
& java -Xms64m -Xmx256m -cp $classpath vn.puzzleonline.server.PuzzleServer "--port=$Port" "--data=$resolvedData"

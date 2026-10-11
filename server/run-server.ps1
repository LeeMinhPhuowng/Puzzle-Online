param(
    [int]$Port = 7778,
    [string]$DataDirectory = "server-data",
    [switch]$Background
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

# Check if port is already in use and stop old process
$existingConns = Get-NetTCPConnection -LocalPort $Port -ErrorAction SilentlyContinue
if ($existingConns) {
    $pidsToKill = $existingConns | Select-Object -ExpandProperty OwningProcess -Unique
    foreach ($procId in $pidsToKill) {
        Write-Host "Port $Port is in use by PID $procId. Stopping old process..."
        Stop-Process -Id $procId -Force -ErrorAction SilentlyContinue
    }
    Start-Sleep -Milliseconds 800
}

Write-Host "Starting server on port $Port with MySQL backend..."
if ($Background) {
    Start-Process -FilePath "java" -ArgumentList "-Xms64m -Xmx256m -cp `"$classpath`" vn.puzzleonline.server.PuzzleServer --port=$Port --data=`"$resolvedData`"" -WorkingDirectory $serverRoot -WindowStyle Hidden
    Write-Host "Server started in background process."
} else {
    & java -Xms64m -Xmx256m -cp $classpath vn.puzzleonline.server.PuzzleServer "--port=$Port" "--data=$resolvedData"
}

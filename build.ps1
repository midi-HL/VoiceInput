<#
.SYNOPSIS
    VoiceInput 构建脚本
.DESCRIPTION
    用于编译、打包和运行 VoiceInput 项目
.PARAMETER Target
    构建目标：build, package, run, clean
.EXAMPLE
    .\build.ps1 -Target build
    .\build.ps1 -Target package
    .\build.ps1 -Target run
    .\build.ps1 -Target clean
#>

param(
    [Parameter(Position = 0)]
    [ValidateSet("build", "package", "run", "clean")]
    [string]$Target = "build"
)

$ErrorActionPreference = "Stop"
$ProjectDir = $PSScriptRoot
$SrcDir = Join-Path $ProjectDir "src\VoiceInput"
$OutputDir = Join-Path $ProjectDir "dist"
$PublishDir = Join-Path $ProjectDir "publish"

# 版本号
$Version = "1.0.0"

function Write-Step {
    param([string]$Message)
    Write-Host "`n>>> $Message" -ForegroundColor Cyan
}

function Write-Success {
    param([string]$Message)
    Write-Host "    $Message" -ForegroundColor Green
}

function Write-Warning {
    param([string]$Message)
    Write-Host "    $Message" -ForegroundColor Yellow
}

function Write-Error {
    param([string]$Message)
    Write-Host "    $Message" -ForegroundColor Red
}

function Test-DotNetSdk {
    Write-Step "检查 .NET SDK"
    try {
        $dotnetVersion = dotnet --version
        Write-Success ".NET SDK 版本: $dotnetVersion"
        return $true
    }
    catch {
        Write-Error "未找到 .NET SDK，请先安装 .NET 8 SDK"
        return $false
    }
}

function Test-InnoSetup {
    Write-Step "检查 Inno Setup"
    
    # 检查常见安装路径
    $innoSetupPaths = @(
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe",
        "C:\Program Files (x86)\Inno Setup 5\ISCC.exe"
    )
    
    foreach ($path in $innoSetupPaths) {
        if (Test-Path $path) {
            Write-Success "找到 Inno Setup: $path"
            return $path
        }
    }
    
    # 检查 PATH
    $iscc = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
    if ($iscc) {
        Write-Success "找到 Inno Setup: $($iscc.Source)"
        return $iscc.Source
    }
    
    Write-Warning "未找到 Inno Setup，无法生成安装包"
    Write-Warning "请从 https://jrsoftware.org/isdl.php 下载并安装 Inno Setup 6"
    return $null
}

function Build-Project {
    Write-Step "编译项目"
    
    # 清理旧的发布目录
    if (Test-Path $PublishDir) {
        Remove-Item $PublishDir -Recurse -Force
    }
    
    # 发布项目
    dotnet publish $SrcDir `
        --configuration Release `
        --output $PublishDir `
        --self-contained true `
        --runtime win-x64 `
        --verbosity minimal
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "编译失败"
        exit 1
    }
    
    Write-Success "编译完成: $PublishDir"
}

function Package-Project {
    Write-Step "打包项目"
    
    # 先编译
    Build-Project
    
    # 创建 dist 目录
    if (-not (Test-Path $OutputDir)) {
        New-Item -ItemType Directory -Path $OutputDir | Out-Null
    }
    
    # 创建便携版 ZIP
    Write-Step "创建便携版"
    $portableZip = Join-Path $OutputDir "VoiceInput-v$Version-portable.zip"
    Compress-Archive -Path "$PublishDir\*" -DestinationPath $portableZip -Force
    Write-Success "便携版: $portableZip"
    
    # 创建安装包
    $innoSetupPath = Test-InnoSetup
    if ($innoSetupPath) {
        Write-Step "创建安装包"
        $issFile = Join-Path $ProjectDir "installer\setup.iss"
        
        # 执行 Inno Setup 编译
        & $innoSetupPath /Q /DVERSION=$Version $issFile
        
        if ($LASTEXITCODE -eq 0) {
            Write-Success "安装包已生成: $OutputDir"
        } else {
            Write-Error "安装包生成失败"
        }
    }
    
    # 生成校验和
    Write-Step "生成校验和"
    $checksums = @()
    Get-ChildItem $OutputDir -File | ForEach-Object {
        $hash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash
        $checksums += "$hash  $($_.Name)"
    }
    $checksumsFile = Join-Path $OutputDir "checksums.txt"
    $checksums | Out-File $checksumsFile -Encoding UTF8
    Write-Success "校验和: $checksumsFile"
}

function Run-Project {
    Write-Step "运行项目"
    
    # 检查是否已编译
    $exePath = Join-Path $PublishDir "VoiceInput.exe"
    if (-not (Test-Path $exePath)) {
        Write-Warning "未找到已发布的程序，先进行编译..."
        Build-Project
    }
    
    Write-Success "启动 VoiceInput..."
    Start-Process $exePath
}

function Clean-Project {
    Write-Step "清理构建产物"
    
    # 清理 publish 目录
    if (Test-Path $PublishDir) {
        Remove-Item $PublishDir -Recurse -Force
        Write-Success "已清理: $PublishDir"
    }
    
    # 清理 dist 目录
    if (Test-Path $OutputDir) {
        Remove-Item $OutputDir -Recurse -Force
        Write-Success "已清理: $OutputDir"
    }
    
    # 清理 bin 和 obj
    $binDir = Join-Path $SrcDir "bin"
    $objDir = Join-Path $SrcDir "obj"
    
    if (Test-Path $binDir) {
        Remove-Item $binDir -Recurse -Force
        Write-Success "已清理: $binDir"
    }
    
    if (Test-Path $objDir) {
        Remove-Item $objDir -Recurse -Force
        Write-Success "已清理: $objDir"
    }
}

# 主逻辑
Write-Host "`n========================================" -ForegroundColor White
Write-Host " VoiceInput 构建脚本 v$Version" -ForegroundColor White
Write-Host "========================================`n" -ForegroundColor White

# 检查 .NET SDK
if (-not (Test-DotNetSdk)) {
    exit 1
}

switch ($Target) {
    "build" {
        Build-Project
    }
    "package" {
        Package-Project
    }
    "run" {
        Run-Project
    }
    "clean" {
        Clean-Project
    }
}

Write-Host "`n完成！`n" -ForegroundColor Green

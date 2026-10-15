<#
.SYNOPSIS
    Atualiza a cópia do Monaco empacotada no Designer (src/Reporting.Designer.Blazor/wwwroot/lib/monaco).

.DESCRIPTION
    O Designer carrega o Monaco do próprio pacote, para o editor de expressões funcionar offline e sem liberar CDN
    na Content-Security-Policy. Só o necessário é copiado: o núcleo do editor, o worker do editor, a fonte de ícones
    e a linguagem SQL (editor de consultas). As demais linguagens e os serviços de linguagem (TypeScript, JSON, CSS,
    HTML) ficam de fora — somam ~7 MB e o Designer não os usa.

    Requer o npm no PATH. O pacote é baixado com `npm pack` (sem executar scripts de instalação) numa pasta
    temporária, e só os arquivos da lista abaixo são copiados.

.EXAMPLE
    ./tools/update-monaco.ps1 -Version 0.45.0
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string] $Version
)

$ErrorActionPreference = 'Stop'

# Se uma versão nova passar a exigir outro arquivo, o editor falha ao abrir no navegador e o console mostra o
# módulo que faltou: acrescente-o aqui.
$files = @(
    'min/vs/loader.js',
    'min/vs/editor/editor.main.js',
    'min/vs/editor/editor.main.css',
    'min/vs/editor/editor.main.nls.js',
    'min/vs/base/worker/workerMain.js',
    'min/vs/base/common/worker/simpleWorker.nls.js',
    'min/vs/base/browser/ui/codicons/codicon/codicon.ttf',
    'min/vs/basic-languages/sql/sql.js',
    'LICENSE',
    'ThirdPartyNotices.txt'
)

$repoRoot = Split-Path -Parent $PSScriptRoot
$target = Join-Path $repoRoot 'src/Reporting.Designer.Blazor/wwwroot/lib/monaco'
$work = Join-Path ([System.IO.Path]::GetTempPath()) "omnireport-monaco-$Version-$([guid]::NewGuid().ToString('n'))"
New-Item -ItemType Directory -Path $work | Out-Null

try {
    Push-Location $work
    try {
        npm pack "monaco-editor@$Version" --silent | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "npm pack monaco-editor@$Version falhou (versão existe? npm no PATH?)." }
    }
    finally {
        Pop-Location
    }

    $tarball = Join-Path $work "monaco-editor-$Version.tgz"
    tar -xzf $tarball -C $work
    if ($LASTEXITCODE -ne 0) { throw "Não foi possível extrair $tarball." }
    $package = Join-Path $work 'package'

    $missing = $files | Where-Object { -not (Test-Path (Join-Path $package $_)) }
    if ($missing) { throw "A versão $Version não tem: $($missing -join ', '). Revise a lista de arquivos deste script." }

    if (Test-Path $target) { Remove-Item -Recurse -Force $target }
    foreach ($file in $files) {
        $relative = $file -replace '^min/', ''
        $destination = Join-Path $target $relative
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $destination) | Out-Null
        Copy-Item (Join-Path $package $file) $destination
    }
    Set-Content -Path (Join-Path $target 'VERSION') -Value $Version -NoNewline

    Write-Host "Monaco $Version copiado para $target."
    Write-Host 'Abra o editor de expressões e o de SQL no sample e confira o console do navegador antes de commitar.'
}
finally {
    Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue
}

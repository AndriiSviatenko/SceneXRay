<#
.SYNOPSIS
    Compiles SceneXRay against every Unity editor installed on this machine.

.DESCRIPTION
    The point of this script is that "does SceneXRay still work on the new Unity?" should be a
    two-minute answer, not an afternoon. It takes the csproj Unity generated for the plugin,
    repoints it at each installed editor, applies the version defines that the asmdef would
    supply there, and compiles. Nothing is written into the project.

    Errors mean the plugin is broken on that editor. Obsolete warnings (CS0618) are the early
    warning: they are what turns into errors one or two releases later, so a new one appearing
    is the signal to add a branch in SceneXRayCompat.

.PARAMETER EditorRoot
    Where Unity Hub keeps editors. Defaults to the standard Windows location.

.PARAMETER ShowObsolete
    List every obsolete-API warning instead of just counting them.

.EXAMPLE
    pwsh ./Tools~/verify-unity-versions.ps1
    pwsh ./Tools~/verify-unity-versions.ps1 -ShowObsolete
#>
[CmdletBinding()]
param(
    [string] $EditorRoot = 'C:\Program Files\Unity\Hub\Editor',
    [switch] $ShowObsolete
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$template = Join-Path $repoRoot 'SceneXRay.Editor.csproj'

if (-not (Test-Path $template)) {
    Write-Error "SceneXRay.Editor.csproj not found. Open the project in Unity once so it generates the C# project, then re-run."
}

# An editor entry in the Hub folder can be metadata only (listed but not installed).
$editors = Get-ChildItem -Path $EditorRoot -Directory -ErrorAction SilentlyContinue |
    Where-Object { Test-Path (Join-Path $_.FullName 'Editor\Data\Managed\UnityEngine\UnityEngine.dll') } |
    Sort-Object Name

if (-not $editors) { Write-Error "No installed editors found under $EditorRoot." }

# Whatever version the template was generated for — every path in it points there.
$templateXml = Get-Content $template -Raw
if ($templateXml -notmatch 'Hub\\Editor\\([^\\]+)\\Editor\\Data') {
    Write-Error "Could not read the editor version out of $template."
}
$templateVersion = $Matches[1]

function Get-VersionDefines([string] $version) {
    # Mirrors versionDefines in SceneXRay.Editor.asmdef. A version expression means "or newer".
    $defines = @()
    if ($version -ge '6000')   { $defines += 'SCENEXRAY_UNITY_6_OR_NEWER' }
    if ($version -ge '6000.3') { $defines += 'SCENEXRAY_UNITY_6_3_OR_NEWER' }
    if ($version -ge '6000.5') { $defines += 'SCENEXRAY_UNITY_6_5_OR_NEWER' }
    return ($defines -join ';') + ';'
}

$temp = Join-Path ([System.IO.Path]::GetTempPath()) ("scenexray-verify-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temp -Force | Out-Null
$failed = @()

try {
    foreach ($editor in $editors) {
        $version = $editor.Name
        Write-Host ""
        Write-Host "=== Unity $version" -ForegroundColor Cyan

        $xml = $templateXml.Replace($templateVersion, $version)
        $xml = $xml -replace '<DefineConstants>', ('<DefineConstants>' + (Get-VersionDefines $version))
        # Analyzers and the exact module set differ per editor and say nothing about our API use.
        $xml = $xml -replace '(?m)^[ \t]*<Analyzer Include="[^"]*"\s*/>\r?\n', ''
        $xml = $xml -replace 'Temp\\bin\\Debug', ('Temp\bin\verify-' + $version)

        # Drop references and sources that do not exist for this editor / were deleted since the
        # template was generated — otherwise the run fails on bookkeeping instead of on real API.
        $xml = [regex]::Replace($xml, '(?s)[ \t]*<Reference Include="[^"]*">.*?</Reference>\r?\n', {
            param($m)
            if ($m.Value -match '<HintPath>([^<]*)</HintPath>' -and -not (Test-Path $Matches[1])) { '' } else { $m.Value }
        })
        $xml = [regex]::Replace($xml, '[ \t]*<Compile Include="([^"]*)"\s*/>\r?\n', {
            param($m)
            $full = Join-Path $repoRoot ($m.Groups[1].Value -replace '\\', '/')
            if (Test-Path $full) { $m.Value } else { '' }
        })

        # Unity regenerates the csproj on its own schedule, so a file added since the last
        # regeneration would silently not be compiled — and the run would report a pass on code
        # it never saw. Add every source under the plugin's editor folder that is not listed.
        $listed = [regex]::Matches($xml, '<Compile Include="([^"]*)"') |
            ForEach-Object { $_.Groups[1].Value.Replace('\', '/') }
        $listedSet = [System.Collections.Generic.HashSet[string]]::new(
            [string[]]$listed, [System.StringComparer]::OrdinalIgnoreCase)

        $editorDir = Join-Path $repoRoot 'Assets/SceneXRay/Editor'
        $missing = Get-ChildItem -Path $editorDir -Filter *.cs -Recurse -File | ForEach-Object {
            $rel = $_.FullName.Substring($repoRoot.Length + 1).Replace('/', '\')
            if (-not $listedSet.Contains($rel.Replace('\', '/'))) { $rel }
        }
        if ($missing) {
            $added = ($missing | ForEach-Object { "    <Compile Include=`"$_`" />" }) -join "`n"
            # -replace has no count argument, so splice it in ahead of the first Compile item.
            $at = $xml.IndexOf('<Compile Include=')
            $lineStart = $xml.LastIndexOf("`n", $at) + 1
            $xml = $xml.Substring(0, $lineStart) + $added + "`n" + $xml.Substring($lineStart)
        }

        $proj = Join-Path $repoRoot ("SceneXRay.Verify-$version.csproj")
        Set-Content -Path $proj -Value $xml -Encoding utf8

        try {
            $log = & dotnet msbuild $proj -nologo -v:m 2>&1 | Out-String
        }
        finally {
            Remove-Item $proj -Force -ErrorAction SilentlyContinue
        }

        $lines = $log -split "`r?`n"
        $errors = $lines | Where-Object { $_ -match ': error ' } | Sort-Object -Unique
        $obsolete = $lines | Where-Object { $_ -match 'warning CS061[89]' } | Sort-Object -Unique

        if ($errors) {
            $failed += $version
            Write-Host ("  FAILED - {0} error(s)" -f $errors.Count) -ForegroundColor Red
            $errors | ForEach-Object { Write-Host "    $_" -ForegroundColor Red }
        }
        else {
            Write-Host ("  OK - {0} obsolete-API warning(s)" -f $obsolete.Count) -ForegroundColor Green
        }

        if ($ShowObsolete -and $obsolete) {
            $obsolete | ForEach-Object { Write-Host "    $_" -ForegroundColor DarkYellow }
        }
    }
}
finally {
    Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host ""
if ($failed) {
    Write-Host ("Broken on: {0}" -f ($failed -join ', ')) -ForegroundColor Red
    exit 1
}

Write-Host "Compiles on every installed editor." -ForegroundColor Green

param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $RawArgs
)

$Mode = "plan"
$OutputDir = Join-Path (Split-Path -Path $PSScriptRoot -Parent) "docs\radical-plans"
$trigger = if ($RawArgs.Count -gt 0 -and $RawArgs[0].ToLowerInvariant() -eq "radical") {
    $RawArgs | Select-Object -Skip 1
} else {
    $RawArgs
}

$goalArgs = @()
for ($i = 0; $i -lt $trigger.Count; $i++) {
    $arg = $trigger[$i]
    if ($arg -like "-Mode=*") {
        $Mode = $arg.Substring(6)
        continue
    }
    if ($arg -eq "-Mode" -and $i + 1 -lt $trigger.Count) {
        $Mode = $trigger[$i + 1]
        $i++
        continue
    }
    if ($arg -like "-OutputDir=*") {
        $OutputDir = $arg.Substring(11)
        continue
    }
    if ($arg -eq "-OutputDir" -and $i + 1 -lt $trigger.Count) {
        $OutputDir = $trigger[$i + 1]
        $i++
        continue
    }
    $goalArgs += $arg
}

if ($Mode -ne "plan" -and $Mode -ne "execute") {
    throw "Invalid -Mode. Use -Mode plan or execute."
}

$goal = if ($goalArgs.Count -gt 0) { $goalArgs -join " " } else { "" }
if ([string]::IsNullOrWhiteSpace($goal)) {
    $goal = Read-Host "Radical-Goal (1 kurzer Satz)"
}

if (-not (Test-Path -Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
}

$outFile = Join-Path $OutputDir ("radical-plan-" + (Get-Date -Format "yyyyMMdd-HHmmss") + ".md")

$content = @"
# Radical Plan (Auto-Generated)

Auslöser: `radical`
Datum: $((Get-Date).ToString('yyyy-MM-dd HH:mm'))
Ziel: $goal

## 1) Breakdown der aktuellen App (Kurz)
- Kernbeobachtung: Standardflow ist aktuell step-by-step (Input -> Setup -> Run -> Review -> Adjust).
- Hauptproblem: Viele manuelle Entscheidungsstellen mit niedriger Produktivität.

## 2) 5 radikale Ideen
1. **Agentic Auto-Captain** – Zielsatz erzeugt komplette Ausführung.
2. **Zero-UI Intake** – Formular- und Tab-basierte Eingabe wird auf Chat reduziert.
3. **Silent Batch Reactor** – Ausführung im Hintergrund nach Opportunitätslogik.
4. **Reality Diff Engine** – automatische Konsistenzkorrektur zwischen Chat, Plan, Execution.
5. **Promptless Operator** – Nutzer gibt nur Intents, KI führt Routineketten autonom aus.

## 3) Beste Idee
**Agentic Auto-Captain** als primärer Umbaupfad (maximaler Paradigmenbruch, hoher Wow, MVP-fähig).

## 4) Build-ready Feature
- Neue Kernservices: `GoalOrchestrator`, `PlanCompiler`, `AutoExecutor`, `PolicyGuard`, `ReflectionLoop`
- Minimale Screens: CaptainChat, ExceptionPulse (nur bei Risiko), FinalDigest.
- Umsetzung: 1) Ziel -> 2) automatischer Plan -> 3) Selbstführung -> 4) Ausnahme-Review -> 5) Kurzbericht.

## 5) Nächster Schritt
- Diese Datei im Repo als Arbeitsgrundlage übernehmen und als Story in den nächsten Sprint übernehmen.
"@

Set-Content -Path $outFile -Value $content -Encoding UTF8
if ($Mode -eq "plan") {
    Write-Host "Plan erstellt: $outFile"
} else {
    Write-Host "Plan erstellt: $outFile"
    Write-Host "Execute-Mode: for local execution, run app path -> trigger with 'radical run <goal>'."
}

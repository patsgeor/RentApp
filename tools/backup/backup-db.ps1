<#
.SYNOPSIS
Παίρνει πλήρες αντίγραφο της βάσης, το κρυπτογραφεί, το επαληθεύει και κρατά
ιστορικό κατά GFS (ημερήσια / εβδομαδιαία / μηνιαία).

.DESCRIPTION
Η ροή: pg_dump → κρυπτογράφηση → δοκιμή ανάγνωσης του κρυπτογραφημένου αρχείου →
αντιγραφή εκτός μηχανήματος → καθαρισμός παλαιών → καταγραφή.

Αν οποιοδήποτε βήμα αποτύχει, το script τερματίζει με κωδικό εξόδου 1 ώστε ο
Χρονοπρογραμματιστής των Windows να δείξει την εργασία ως αποτυχημένη.

.PARAMETER ConnectionString
Παρακάμπτει το αποθηκευμένο connection string. Για δοκιμές σε άλλη βάση.

.PARAMETER SkipMirror
Παράλειψη της αντιγραφής στον δεύτερο φάκελο (OneDrive).

.EXAMPLE
.\backup-db.ps1

.EXAMPLE
.\backup-db.ps1 -ConnectionString 'Host=localhost;Database=RentDb_backuptest;Username=postgres;Password=…'
#>
[CmdletBinding()]
param(
    [string]$ConnectionString,
    [string]$Passphrase,
    [switch]$SkipMirror
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'lib\common.ps1')

$started = Get-Date
$tempDump = $null
$tempCheck = $null

try {
    if (-not $ConnectionString) { $ConnectionString = Read-Secret 'connection' }
    if (-not $Passphrase)       { $Passphrase = Read-Secret 'passphrase' }

    $uri = Resolve-UnpooledUri (ConvertTo-PgUri $ConnectionString)
    Assert-PgVersion -Uri $uri

    if (-not (Test-Path $Settings.BackupRoot)) {
        New-Item -ItemType Directory -Path $Settings.BackupRoot -Force | Out-Null
    }

    $name   = 'rentapp-{0}.dump.enc' -f $started.ToString('yyyyMMdd-HHmm')
    $target = Join-Path $Settings.BackupRoot $name

    # ── 1. pg_dump ────────────────────────────────────────────────────────────
    $tempDump = Join-Path $env:TEMP ([IO.Path]::GetRandomFileName() + '.dump')

    Write-Log 'Έναρξη αντιγράφου…'

    # -Fc: custom μορφή, ώστε να επιτρέπει επιλεκτική και παράλληλη επαναφορά.
    # --no-owner/--no-privileges: οι ρόλοι της Neon δεν υπάρχουν αλλού, και χωρίς
    #   αυτά η επαναφορά γεμίζει σφάλματα ALTER OWNER.
    $dump = Invoke-Exe -Path (Get-PgTool 'pg_dump') -Arguments @(
        '-Fc', '-Z', '1', '--no-owner', '--no-privileges', '--no-tablespaces',
        '-d', $uri, '-f', $tempDump)

    if ($dump.ExitCode -ne 0) { throw "Το pg_dump απέτυχε: $($dump.Output -join '; ')" }

    $dumpSize = (Get-Item $tempDump).Length
    if ($dumpSize -lt 1024) { throw "Το αντίγραφο είναι ύποπτα μικρό ($dumpSize bytes)." }

    Write-Log ('Το dump ολοκληρώθηκε: {0:N1} MB' -f ($dumpSize / 1MB))

    # ── 2. κρυπτογράφηση ──────────────────────────────────────────────────────
    Protect-BackupFile -InputPath $tempDump -OutputPath $target -Passphrase $Passphrase

    # ── 3. επαλήθευση στην ίδια εκτέλεση ──────────────────────────────────────
    # Αποκρυπτογραφούμε και ζητάμε από το pg_restore να διαβάσει το περιεχόμενο.
    # Έτσι ένα χαλασμένο αντίγραφο δεν περνά απαρατήρητο μέχρι την ημέρα που θα
    # το χρειαστούμε.
    $tempCheck = Join-Path $env:TEMP ([IO.Path]::GetRandomFileName() + '.dump')
    Unprotect-BackupFile -InputPath $target -OutputPath $tempCheck -Passphrase $Passphrase

    $list = Invoke-Exe -Path (Get-PgTool 'pg_restore') -Arguments @('--list', $tempCheck)
    if ($list.ExitCode -ne 0) {
        Remove-Item $target -Force -ErrorAction SilentlyContinue
        throw "Το κρυπτογραφημένο αντίγραφο δεν διαβάζεται, διαγράφηκε: $($list.Output -join '; ')"
    }

    $tableCount = ([regex]::Matches(($list.Output -join "`n"), 'TABLE DATA')).Count
    Write-Log "Επαλήθευση εντάξει: $tableCount πίνακες με δεδομένα."

    # ── 4. αντίγραφο εκτός μηχανήματος ────────────────────────────────────────
    if (-not $SkipMirror -and $Settings.MirrorRoot) {
        if (-not (Test-Path $Settings.MirrorRoot)) {
            New-Item -ItemType Directory -Path $Settings.MirrorRoot -Force | Out-Null
        }
        Copy-Item $target -Destination (Join-Path $Settings.MirrorRoot $name) -Force
        Write-Log "Αντιγράφηκε στο $($Settings.MirrorRoot)"
    }

    # ── 5. διατήρηση ──────────────────────────────────────────────────────────
    Remove-ExpiredBackups -Folder $Settings.BackupRoot
    if (-not $SkipMirror -and $Settings.MirrorRoot) { Remove-ExpiredBackups -Folder $Settings.MirrorRoot }

    $elapsed = (Get-Date) - $started
    $finalSize = (Get-Item $target).Length

    Write-Log ('Ολοκληρώθηκε: {0} · {1:N1} MB · {2:N0} δευτερόλεπτα' -f $name, ($finalSize / 1MB), $elapsed.TotalSeconds)

    Set-Content -Path (Join-Path $Settings.BackupRoot 'last-success.txt') `
                -Value $started.ToString('yyyy-MM-dd HH:mm:ss') -Encoding ASCII

    exit 0
}
catch {
    Write-Log $_.Exception.Message -Level ERROR
    exit 1
}
finally {
    # Τα προσωρινά αρχεία είναι ΑΚΡΥΠΤΟΓΡΑΦΗΤΑ δεδομένα πελατών — φεύγουν πάντα.
    foreach ($temp in @($tempDump, $tempCheck)) {
        if ($temp -and (Test-Path $temp)) { Remove-Item $temp -Force -ErrorAction SilentlyContinue }
    }
}

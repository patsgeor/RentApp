<#
.SYNOPSIS
Επαναφέρει ένα κρυπτογραφημένο αντίγραφο σε βάση που δηλώνεται ρητά.

.DESCRIPTION
Αποκρυπτογραφεί το αρχείο (ελέγχοντας πρώτα την ακεραιότητά του) και εκτελεί
pg_restore στον στόχο. Ο στόχος δίνεται πάντα ρητά — δεν υπάρχει προεπιλογή που
να δείχνει στην παραγωγή.

Προτεινόμενη πρακτική για την παραγωγή: δημιούργησε ΝΕΟ branch στη Neon,
επανάφερε εκεί, επαλήθευσε, και μετά άλλαξε το connection string της εφαρμογής.
Έτσι τα υπάρχοντα δεδομένα μένουν άθικτα μέχρι να βεβαιωθείς.

.PARAMETER BackupFile
Το αρχείο .dump.enc. Αν παραλειφθεί, χρησιμοποιείται το νεότερο του φακέλου.

.PARAMETER Target
'Local' για την τοπική βάση δοκιμών, ή 'Custom' με -ConnectionString.

.PARAMETER Database
Όνομα βάσης όταν ο στόχος είναι Local. Δημιουργείται αν δεν υπάρχει.

.PARAMETER Recreate
Διαγράφει και ξαναφτιάχνει τη βάση-στόχο πριν την επαναφορά (μόνο για Local).

.EXAMPLE
.\restore-db.ps1 -Target Local -Database RentDb_restoretest -Recreate

.EXAMPLE
.\restore-db.ps1 -BackupFile C:\…\rentapp-20261003-0300.dump.enc -Target Custom -ConnectionString 'postgresql://…'
#>
[CmdletBinding()]
param(
    [string]$BackupFile,

    [Parameter(Mandatory)]
    [ValidateSet('Local', 'Custom')]
    [string]$Target,

    [string]$Database,
    [string]$ConnectionString,
    [string]$Passphrase,
    [switch]$Recreate,
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'lib\common.ps1')

$tempDump = $null

try {
    # ── Ποιο αρχείο ───────────────────────────────────────────────────────────
    if (-not $BackupFile) {
        $latest = Get-ChildItem -Path $Settings.BackupRoot -Filter 'rentapp-*.dump.enc' -File -ErrorAction SilentlyContinue |
                  Sort-Object Name -Descending | Select-Object -First 1
        if (-not $latest) { throw "Δεν βρέθηκε αντίγραφο στο $($Settings.BackupRoot)." }
        $BackupFile = $latest.FullName
    }

    if (-not (Test-Path $BackupFile)) { throw "Δεν βρέθηκε το αρχείο '$BackupFile'." }

    # ── Πού ───────────────────────────────────────────────────────────────────
    if ($Target -eq 'Local') {
        if (-not $Database) { throw 'Χρειάζεται -Database όταν ο στόχος είναι Local.' }

        $adminUri = 'postgresql://{0}@{1}:{2}/postgres' -f $Settings.LocalUser, $Settings.LocalHost, $Settings.LocalPort
        $uri      = 'postgresql://{0}@{1}:{2}/{3}' -f $Settings.LocalUser, $Settings.LocalHost, $Settings.LocalPort, $Database

        if (-not $env:PGPASSWORD) {
            $securePwd = Read-Host "Κωδικός του χρήστη '$($Settings.LocalUser)' στον τοπικό PostgreSQL" -AsSecureString
            $env:PGPASSWORD = [Runtime.InteropServices.Marshal]::PtrToStringBSTR(
                [Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePwd))
        }

        if ($Recreate) {
            Write-Log "Επαναδημιουργία της τοπικής βάσης '$Database'…"
            Invoke-PgSql -Uri $adminUri -Sql "drop database if exists ""$Database""" | Out-Null
        }

        $exists = Invoke-PgSql -Uri $adminUri -Tuples -Sql "select 1 from pg_database where datname = '$Database'"
        if (-not $exists) {
            Invoke-PgSql -Uri $adminUri -Sql "create database ""$Database""" | Out-Null
            Write-Log "Δημιουργήθηκε η βάση '$Database'."
        }
    }
    else {
        if (-not $ConnectionString) { throw 'Χρειάζεται -ConnectionString όταν ο στόχος είναι Custom.' }
        $uri = Resolve-UnpooledUri (ConvertTo-PgUri $ConnectionString)

        # Φρένο ασφαλείας: αν ο στόχος είναι η ίδια βάση με το αποθηκευμένο
        # connection string, πρόκειται για γράψιμο πάνω στην παραγωγή.
        $storedUri = $null
        try { $storedUri = Resolve-UnpooledUri (ConvertTo-PgUri (Read-Secret 'connection')) } catch { }

        if ($storedUri -and ($storedUri -eq $uri) -and -not $Force) {
            Write-Host ''
            Write-Host 'ΠΡΟΣΟΧΗ: ο στόχος είναι η ΠΑΡΑΓΩΓΙΚΗ βάση.' -ForegroundColor Red
            Write-Host 'Τα υπάρχοντα δεδομένα θα αντικατασταθούν από το αντίγραφο.'
            Write-Host 'Προτιμότερο: επαναφορά σε νέο branch της Neon και αλλαγή connection string.'
            Write-Host ''
            $answer = Read-Host "Γράψε ΕΠΑΝΑΦΟΡΑ για να συνεχίσεις"
            if ($answer -ne 'ΕΠΑΝΑΦΟΡΑ') { throw 'Ακυρώθηκε.' }
        }
    }

    # ── Αποκρυπτογράφηση ──────────────────────────────────────────────────────
    if (-not $Passphrase) { $Passphrase = Read-Secret 'passphrase' }

    $tempDump = Join-Path $env:TEMP ([IO.Path]::GetRandomFileName() + '.dump')

    Write-Log "Αποκρυπτογράφηση του $(Split-Path $BackupFile -Leaf)…"
    Unprotect-BackupFile -InputPath $BackupFile -OutputPath $tempDump -Passphrase $Passphrase

    # ── Επαναφορά ─────────────────────────────────────────────────────────────
    Write-Log 'Εκτέλεση pg_restore…'

    # --no-owner/--no-privileges: οι ρόλοι της πηγής δεν υπάρχουν στον στόχο.
    # --clean --if-exists: καθαρίζει αντικείμενα που ήδη υπάρχουν, ώστε η
    #   επαναφορά πάνω σε μη κενή βάση να μη σκοντάφτει σε συγκρούσεις.
    $restore = Invoke-Exe -Path (Get-PgTool 'pg_restore') -Arguments @(
        '--no-owner', '--no-privileges', '--clean', '--if-exists',
        '-j', '4', '-d', $uri, $tempDump)

    # Το pg_restore επιστρέφει μη μηδενικό και για αθώες προειδοποιήσεις
    # (π.χ. "does not exist, skipping" με το --if-exists). Ξεχωρίζουμε τα σοβαρά.
    $errors = $restore.Output | Where-Object { $_ -match 'error:' -and $_ -notmatch 'does not exist' }

    if ($errors) {
        $errors | ForEach-Object { Write-Log $_ -Level WARN }
        throw "Η επαναφορά ολοκληρώθηκε με $($errors.Count) σφάλματα — έλεγξέ τα πριν τη χρησιμοποιήσεις."
    }

    Write-Log 'Η επαναφορά ολοκληρώθηκε.'

    # Σύντομη σύνοψη, για να φαίνεται αμέσως ότι υπάρχουν δεδομένα.
    $summary = Invoke-PgSql -Uri $uri -Tuples -Sql @'
select 'Εταιρείες', count(*) from "Tenants"
union all select 'Πελάτες', count(*) from "Customers"
union all select 'Πάγια', count(*) from "Assets"
union all select 'Συμβόλαια', count(*) from "Contracts"
union all select 'Δόσεις', count(*) from "Installments"
union all select 'Πληρωμές', count(*) from "Payments"
'@
    Write-Host ''
    $summary | ForEach-Object { Write-Host ('  {0}' -f ($_ -replace '\|', ': ')) }
    Write-Host ''

    exit 0
}
catch {
    Write-Log $_.Exception.Message -Level ERROR
    exit 1
}
finally {
    if ($tempDump -and (Test-Path $tempDump)) { Remove-Item $tempDump -Force -ErrorAction SilentlyContinue }
}

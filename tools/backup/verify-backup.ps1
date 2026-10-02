<#
.SYNOPSIS
Άσκηση επαναφοράς: επαναφέρει το τελευταίο αντίγραφο σε προσωρινή τοπική βάση,
μετρά τις γραμμές των βασικών πινάκων και τη διαγράφει.

.DESCRIPTION
Ένα αντίγραφο που δεν έχει δοκιμαστεί δεν είναι αντίγραφο. Τρέξε αυτό μία φορά
τον μήνα — είναι ο μόνος τρόπος να ξέρεις ότι η διαδικασία δουλεύει πριν τη
χρειαστείς στα αλήθεια.

.PARAMETER BackupFile
Συγκεκριμένο αρχείο. Αν παραλειφθεί, χρησιμοποιείται το νεότερο.

.PARAMETER KeepDatabase
Διατηρεί τη δοκιμαστική βάση μετά τον έλεγχο, για να την κοιτάξεις.

.EXAMPLE
.\verify-backup.ps1
#>
[CmdletBinding()]
param(
    [string]$BackupFile,
    [string]$Passphrase,
    [switch]$KeepDatabase
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'lib\common.ps1')

$db = $Settings.DrillDatabase
$adminUri = 'postgresql://{0}@{1}:{2}/postgres' -f $Settings.LocalUser, $Settings.LocalHost, $Settings.LocalPort

try {
    if (-not $BackupFile) {
        $latest = Get-ChildItem -Path $Settings.BackupRoot -Filter 'rentapp-*.dump.enc' -File -ErrorAction SilentlyContinue |
                  Sort-Object Name -Descending | Select-Object -First 1
        if (-not $latest) { throw "Δεν βρέθηκε αντίγραφο στο $($Settings.BackupRoot)." }
        $BackupFile = $latest.FullName
    }

    $age = (Get-Date) - (Get-Item $BackupFile).LastWriteTime
    Write-Log "Άσκηση επαναφοράς με: $(Split-Path $BackupFile -Leaf) (ηλικία $([int]$age.TotalDays) ημερών)"

    if (-not $env:PGPASSWORD) {
        $securePwd = Read-Host "Κωδικός του χρήστη '$($Settings.LocalUser)' στον τοπικό PostgreSQL" -AsSecureString
        $env:PGPASSWORD = [Runtime.InteropServices.Marshal]::PtrToStringBSTR(
            [Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePwd))
    }

    $restoreArgs = @{
        BackupFile = $BackupFile
        Target     = 'Local'
        Database   = $db
        Recreate   = $true
    }
    if ($Passphrase) { $restoreArgs['Passphrase'] = $Passphrase }

    & (Join-Path $PSScriptRoot 'restore-db.ps1') @restoreArgs
    if ($LASTEXITCODE -ne 0) { throw 'Η επαναφορά απέτυχε — το αντίγραφο ΔΕΝ είναι αξιόπιστο.' }

    # ── Έλεγχοι λογικής, όχι μόνο «έτρεξε χωρίς σφάλμα» ───────────────────────
    $uri = 'postgresql://{0}@{1}:{2}/{3}' -f $Settings.LocalUser, $Settings.LocalHost, $Settings.LocalPort, $db

    $tables = Invoke-PgSql -Uri $uri -Tuples -Sql @'
select count(*) from information_schema.tables
where table_schema = 'public' and table_type = 'BASE TABLE'
'@

    $counts = Invoke-PgSql -Uri $uri -Tuples -Sql @'
select 'Tenants', count(*) from "Tenants"
union all select 'Customers', count(*) from "Customers"
union all select 'Assets', count(*) from "Assets"
union all select 'Contracts', count(*) from "Contracts"
union all select 'Installments', count(*) from "Installments"
union all select 'Payments', count(*) from "Payments"
union all select 'PaymentInstallments', count(*) from "PaymentInstallments"
'@

    Write-Host ''
    Write-Host "Πίνακες στο σχήμα public: $tables"
    Write-Host ''

    # Κρίσιμοι πίνακες: αν αυτοί είναι άδειοι, το αντίγραφο δεν αξίζει τίποτα.
    # Οι υπόλοιποι μπορεί νόμιμα να είναι άδειοι σε νέα εγκατάσταση (π.χ. καμία
    # δόση αν δεν έχει ακόμη συναφθεί συμβόλαιο), οπότε απλώς σημειώνονται.
    $critical = @('Tenants', 'Customers', 'Assets')
    $empty = @()
    $problems = @()

    foreach ($row in $counts) {
        $name, $count = $row -split '\|'
        Write-Host ('  {0,-22} {1,8}' -f $name, $count)
        if ([int]$count -eq 0) {
            $empty += $name
            if ($critical -contains $name) { $problems += "ο κρίσιμος πίνακας $name είναι κενός" }
        }
    }
    Write-Host ''

    if ([int]$tables -lt 20) { $problems += "μόνο $tables πίνακες — αναμένονταν τουλάχιστον 20" }

    if ($empty) { Write-Log ("Κενοί πίνακες: {0}" -f ($empty -join ', ')) -Level WARN }

    if ($problems) {
        Write-Log ("Η άσκηση ΑΠΕΤΥΧΕ: {0}" -f ($problems -join ' · ')) -Level ERROR
        Write-Host 'Η επαναφορά έτρεξε, αλλά τα δεδομένα δεν φαίνονται πλήρη. Έλεγξέ το.' -ForegroundColor Yellow
        exit 2
    }

    Write-Log 'Η άσκηση επαναφοράς πέτυχε: το αντίγραφο είναι πλήρες και αναγνώσιμο.'
    exit 0
}
catch {
    Write-Log $_.Exception.Message -Level ERROR
    exit 1
}
finally {
    if (-not $KeepDatabase) {
        Invoke-PgSql -Uri $adminUri -Sql "drop database if exists ""$db""" | Out-Null
        Write-Log "Η δοκιμαστική βάση '$db' διαγράφηκε."
    }
}

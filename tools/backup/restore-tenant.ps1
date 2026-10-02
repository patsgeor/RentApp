<#
.SYNOPSIS
Επαναφέρει τα δεδομένα ΜΙΑΣ εταιρείας (tenant) από ένα αντίγραφο, χωρίς να
πειράξει τις υπόλοιπες.

.DESCRIPTION
Η εφαρμογή είναι πολυ-μισθωτική: όλες οι εταιρείες μοιράζονται τους ίδιους
πίνακες και ξεχωρίζουν από τη στήλη TenantId. Μια κλασική επαναφορά ολόκληρης
της βάσης θα γύριζε πίσω και τις άλλες εταιρείες, σβήνοντας δουλειά ημερών.

Η ροή εδώ:
  1. Το αντίγραφο επαναφέρεται σε ΠΡΟΣΩΡΙΝΗ τοπική βάση (η παραγωγή δεν αγγίζεται).
  2. Από εκεί εξάγονται σε αρχεία CSV μόνο οι γραμμές της συγκεκριμένης εταιρείας.
  3. Στον στόχο, μέσα σε ΜΙΑ συναλλαγή, σβήνονται οι τρέχουσες γραμμές της
     εταιρείας και μπαίνουν οι παλιές. Αν κάτι αποτύχει, δεν αλλάζει τίποτα.
  4. Γίνεται έλεγχος ότι τα πλήθη γραμμών ταιριάζουν.

ΤΙ ΔΕΝ ΓΥΡΙΖΕΙ ΠΙΣΩ:
  • Τα αρχεία στο Cloudflare R2. Αν μια φωτογραφία διαγράφηκε μετά το αντίγραφο,
    η εγγραφή επανέρχεται αλλά το αρχείο λείπει.
  • Τα AuditLogs και ErrorLogs: είναι ιστορικό συμβάντων και δεν «γυρίζει πίσω».

.PARAMETER TenantName
Μέρος της επωνυμίας της εταιρείας, όπως είναι μέσα στο αντίγραφο.

.PARAMETER TenantId
Εναλλακτικά, το ακριβές αναγνωριστικό (GUID).

.PARAMETER TargetConnectionString
Πού γράφουμε. Δίνεται πάντα ρητά — δεν υπάρχει προεπιλογή που να δείχνει στην
παραγωγή κατά λάθος.

.PARAMETER DryRun
Δείχνει τι θα γίνει (πλήθη γραμμών ανά πίνακα) και σταματά.

.EXAMPLE
.\restore-tenant.ps1 -TenantName 'Hellenic' -TargetConnectionString 'postgresql://…' -DryRun

.EXAMPLE
.\restore-tenant.ps1 -BackupFile C:\…\rentapp-20260903-0300.dump.enc -TenantName 'Hellenic' -TargetConnectionString 'postgresql://…'
#>
[CmdletBinding()]
param(
    [string]$BackupFile,
    [string]$TenantName,
    [string]$TenantId,

    [Parameter(Mandatory)]
    [string]$TargetConnectionString,

    [string]$ScratchDatabase = 'RentDb_tenant_source',
    [string]$Passphrase,
    [switch]$DryRun,
    [switch]$KeepScratch,
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'lib\common.ps1')

# ──────────────────────────────────────────────────────────────────────────────
# Η σειρά είναι η σειρά ΕΙΣΑΓΩΓΗΣ: πρώτα οι γονείς, μετά τα παιδιά. Η διαγραφή
# γίνεται με την αντίστροφη. Το φίλτρο δείχνει πώς εντοπίζονται οι γραμμές της
# εταιρείας: οι περισσότεροι πίνακες έχουν TenantId, οι υπόλοιποι φτάνουν σε
# αυτήν μέσω του γονέα τους.
# ──────────────────────────────────────────────────────────────────────────────
$Plan = @(
    @{ Table = 'Tenants';               Where = '"Id" = ''{0}''' }
    @{ Table = 'AspNetUsers';           Where = '"TenantId" = ''{0}''' }
    @{ Table = 'Members';               Where = '"Id" in (select "Id" from "AspNetUsers" where "TenantId" = ''{0}'')' }
    @{ Table = 'AspNetUserRoles';       Where = '"UserId" in (select "Id" from "AspNetUsers" where "TenantId" = ''{0}'')' }
    @{ Table = 'AspNetUserClaims';      Where = '"UserId" in (select "Id" from "AspNetUsers" where "TenantId" = ''{0}'')' }
    @{ Table = 'AspNetUserLogins';      Where = '"UserId" in (select "Id" from "AspNetUsers" where "TenantId" = ''{0}'')' }
    @{ Table = 'AspNetUserTokens';      Where = '"UserId" in (select "Id" from "AspNetUsers" where "TenantId" = ''{0}'')' }
    @{ Table = 'AssetTypes';            Where = '"TenantId" = ''{0}''' }
    @{ Table = 'AssetTypeFields';       Where = '"TenantId" = ''{0}''' }
    @{ Table = 'AssetTypeFieldOptions'; Where = '"TenantId" = ''{0}''' }
    @{ Table = 'Customers';             Where = '"TenantId" = ''{0}''' }
    @{ Table = 'Contacts';              Where = '"TenantId" = ''{0}''' }
    @{ Table = 'Assets';                Where = '"TenantId" = ''{0}''' }
    @{ Table = 'AssetAttributeValues';  Where = '"TenantId" = ''{0}''' }
    @{ Table = 'Photos';                Where = '"TenantId" = ''{0}''' }
    @{ Table = 'Contracts';             Where = '"TenantId" = ''{0}''' }
    @{ Table = 'ContractAssets';        Where = '"ContractId" in (select "Id" from "Contracts" where "TenantId" = ''{0}'')' }
    @{ Table = 'Installments';          Where = '"TenantId" = ''{0}''' }
    @{ Table = 'Payments';              Where = '"TenantId" = ''{0}''' }
    @{ Table = 'PaymentInstallments';   Where = '"TenantId" = ''{0}''' }
    @{ Table = 'PaymentAssets';         Where = '"TenantId" = ''{0}''' }
    @{ Table = 'FileAttachments';       Where = '"TenantId" = ''{0}''' }
    @{ Table = 'MemberInvites';         Where = '"TenantId" = ''{0}''' }
    @{ Table = 'PlanUpgradeRequests';   Where = '"TenantId" = ''{0}''' }
)

$exportDir = $null

try {
    $targetUri = Resolve-UnpooledUri (ConvertTo-PgUri $TargetConnectionString)

    if (-not $TenantName -and -not $TenantId) {
        throw 'Δώσε -TenantName ή -TenantId.'
    }

    # ── 1. Το αντίγραφο σε προσωρινή τοπική βάση ─────────────────────────────
    if (-not $BackupFile) {
        $latest = Get-ChildItem -Path $Settings.BackupRoot -Filter 'rentapp-*.dump.enc' -File -ErrorAction SilentlyContinue |
                  Sort-Object Name -Descending | Select-Object -First 1
        if (-not $latest) { throw "Δεν βρέθηκε αντίγραφο στο $($Settings.BackupRoot)." }
        $BackupFile = $latest.FullName
    }

    Write-Log "Αντίγραφο: $(Split-Path $BackupFile -Leaf)"

    if (-not $env:PGPASSWORD) {
        $securePwd = Read-Host "Κωδικός του χρήστη '$($Settings.LocalUser)' στον τοπικό PostgreSQL" -AsSecureString
        $env:PGPASSWORD = [Runtime.InteropServices.Marshal]::PtrToStringBSTR(
            [Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePwd))
    }

    $restoreArgs = @{
        BackupFile = $BackupFile
        Target     = 'Local'
        Database   = $ScratchDatabase
        Recreate   = $true
    }
    if ($Passphrase) { $restoreArgs['Passphrase'] = $Passphrase }

    & (Join-Path $PSScriptRoot 'restore-db.ps1') @restoreArgs | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Η επαναφορά του αντιγράφου στην προσωρινή βάση απέτυχε.' }

    $sourceUri = 'postgresql://{0}@{1}:{2}/{3}' -f $Settings.LocalUser, $Settings.LocalHost, $Settings.LocalPort, $ScratchDatabase

    # ── 2. Ποια εταιρεία ─────────────────────────────────────────────────────
    if ($TenantId) {
        $filter = """Id"" = '$TenantId'"
    }
    else {
        $escaped = $TenantName.Replace("'", "''")
        $filter = """Name"" ilike '%$escaped%'"
    }

    $matches = @(Invoke-PgSql -Uri $sourceUri -Tuples -Sql "select ""Id"", ""Name"" from ""Tenants"" where $filter order by ""Name""")

    if ($matches.Count -eq 0) { throw 'Δεν βρέθηκε εταιρεία με αυτά τα στοιχεία μέσα στο αντίγραφο.' }
    if ($matches.Count -gt 1) {
        Write-Host ''
        Write-Host 'Ταιριάζουν περισσότερες από μία εταιρείες — ξαναδώσε με -TenantId:' -ForegroundColor Yellow
        $matches | ForEach-Object { Write-Host "  $_" }
        throw 'Διφορούμενη επιλογή εταιρείας.'
    }

    $tid, $tname = $matches[0] -split '\|', 2
    Write-Log "Εταιρεία: $tname ($tid)"

    # ── 3. Τι θα γίνει ───────────────────────────────────────────────────────
    Write-Host ''
    Write-Host ('{0,-24} {1,10} {2,10}' -f 'Πίνακας', 'αντίγραφο', 'τώρα') -ForegroundColor Cyan
    Write-Host ('─' * 48)

    $work = @()
    foreach ($step in $Plan) {
        $where = $step.Where -f $tid
        $srcCount = [int](Invoke-PgSql -Uri $sourceUri -Tuples -Sql "select count(*) from ""$($step.Table)"" where $where" | Select-Object -First 1)
        $dstCount = [int](Invoke-PgSql -Uri $targetUri -Tuples -Sql "select count(*) from ""$($step.Table)"" where $where" | Select-Object -First 1)

        Write-Host ('{0,-24} {1,10} {2,10}' -f $step.Table, $srcCount, $dstCount)
        $work += [pscustomobject]@{ Table = $step.Table; Where = $where; Source = $srcCount; Target = $dstCount }
    }

    Write-Host ('─' * 48)
    Write-Host ('{0,-24} {1,10} {2,10}' -f 'ΣΥΝΟΛΟ', ($work | Measure-Object Source -Sum).Sum, ($work | Measure-Object Target -Sum).Sum)
    Write-Host ''

    if ($DryRun) {
        Write-Log 'Δοκιμαστική εκτέλεση (-DryRun): δεν άλλαξε τίποτα.'
        exit 0
    }

    # ── 4. Επιβεβαίωση ───────────────────────────────────────────────────────
    if (-not $Force) {
        Write-Host 'Οι τρέχουσες γραμμές ΑΥΤΗΣ της εταιρείας θα διαγραφούν και θα' -ForegroundColor Yellow
        Write-Host 'αντικατασταθούν από εκείνες του αντιγράφου. Οι άλλες εταιρείες δεν θίγονται.' -ForegroundColor Yellow
        Write-Host ''
        $answer = Read-Host "Γράψε την επωνυμία για επιβεβαίωση [$tname]"
        if ($answer -ne $tname) { throw 'Ακυρώθηκε.' }
    }

    # ── 5. Εξαγωγή των γραμμών σε CSV ────────────────────────────────────────
    $exportDir = Join-Path $env:TEMP ('rentapp-tenant-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $exportDir -Force | Out-Null

    $script = New-Object Text.StringBuilder

    # Διαγραφή με την ΑΝΤΙΣΤΡΟΦΗ σειρά (πρώτα τα παιδιά), αλλιώς τα ξένα κλειδιά
    # μπλοκάρουν τη διαγραφή.
    [void]$script.AppendLine('-- Διαγραφή των τρεχουσών γραμμών της εταιρείας')
    for ($i = $work.Count - 1; $i -ge 0; $i--) {
        # Οι παρενθέσεις είναι απαραίτητες: μέσα σε κλήση μεθόδου το κόμμα
        # χωρίζει ορίσματα, οπότε χωρίς αυτές ο τελεστής -f έπαιρνε μόνο το πρώτο.
        [void]$script.AppendLine(('delete from "{0}" where {1};' -f $work[$i].Table, $work[$i].Where))
    }

    [void]$script.AppendLine('')
    [void]$script.AppendLine('-- Εισαγωγή των γραμμών του αντιγράφου')

    foreach ($item in $work) {
        if ($item.Source -eq 0) { continue }

        # Οι στήλες διαβάζονται από τον ΣΤΟΧΟ: αν το αντίγραφο είναι από παλαιότερο
        # σχήμα και λείπει στήλη, η εξαγωγή θα αποτύχει εδώ, με σαφές μήνυμα.
        $columns = @(Invoke-PgSql -Uri $targetUri -Tuples -Sql @"
select string_agg(format('%I', column_name), ', ' order by ordinal_position)
from information_schema.columns
where table_schema = 'public' and table_name = '$($item.Table)'
"@ | Select-Object -First 1)

        $csv = Join-Path $exportDir "$($item.Table).csv"

        $exportSql = "\copy (select $columns from ""$($item.Table)"" where $($item.Where)) to '$($csv -replace '\\', '/')' with (format csv)"
        Invoke-PgSql -Uri $sourceUri -Sql $exportSql | Out-Null

        [void]$script.AppendLine("\copy ""$($item.Table)"" ($columns) from '$($csv -replace '\\', '/')' with (format csv)")
    }

    # ── 6. Εφαρμογή σε μία συναλλαγή ─────────────────────────────────────────
    $scriptFile = Join-Path $exportDir 'apply.sql'
    [IO.File]::WriteAllText($scriptFile, $script.ToString(), (New-Object Text.UTF8Encoding($false)))

    Write-Log 'Εφαρμογή στον στόχο, μέσα σε μία συναλλαγή…'

    $env:PGCLIENTENCODING = 'UTF8'
    $apply = Invoke-Exe -Path (Get-PgTool 'psql') -Arguments @(
        '-d', $targetUri, '-v', 'ON_ERROR_STOP=1', '--single-transaction', '-f', $scriptFile)

    if ($apply.ExitCode -ne 0) {
        throw "Η επαναφορά ακυρώθηκε και ΔΕΝ άλλαξε τίποτα: $($apply.Output -join '; ')"
    }

    # ── 7. Επαλήθευση ────────────────────────────────────────────────────────
    $mismatch = @()
    foreach ($item in $work) {
        $after = [int](Invoke-PgSql -Uri $targetUri -Tuples -Sql "select count(*) from ""$($item.Table)"" where $($item.Where)" | Select-Object -First 1)
        if ($after -ne $item.Source) { $mismatch += "$($item.Table): $after αντί για $($item.Source)" }
    }

    if ($mismatch) {
        throw ("Ασυμφωνία μετά την επαναφορά: {0}" -f ($mismatch -join ', '))
    }

    Write-Log "Η εταιρεία '$tname' επανήλθε στην κατάσταση του αντιγράφου. Οι υπόλοιπες δεν άλλαξαν."
    Write-Host ''
    Write-Host 'Θυμήσου: τα αρχεία στο R2 (φωτογραφίες, δικαιολογητικά) δεν γυρίζουν πίσω.' -ForegroundColor Yellow
    Write-Host ''

    exit 0
}
catch {
    Write-Log $_.Exception.Message -Level ERROR
    exit 1
}
finally {
    # Τα CSV περιέχουν ΑΚΡΥΠΤΟΓΡΑΦΗΤΑ προσωπικά δεδομένα — φεύγουν πάντα.
    if ($exportDir -and (Test-Path $exportDir)) {
        Remove-Item $exportDir -Recurse -Force -ErrorAction SilentlyContinue
    }

    if (-not $KeepScratch) {
        $adminUri = 'postgresql://{0}@{1}:{2}/postgres' -f $Settings.LocalUser, $Settings.LocalHost, $Settings.LocalPort
        try {
            Invoke-PgSql -Uri $adminUri -Sql "drop database if exists ""$ScratchDatabase""" | Out-Null
            Write-Log "Η προσωρινή βάση '$ScratchDatabase' διαγράφηκε."
        }
        catch { }
    }
}

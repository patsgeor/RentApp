<#
.SYNOPSIS
Αποθηκεύει, μία φορά, το connection string της βάσης και τον κωδικό κρυπτογράφησης.

.DESCRIPTION
Οι τιμές φυλάσσονται στο %LOCALAPPDATA%\RentApp\backup\ κρυπτογραφημένες με DPAPI:
ανοίγουν μόνο από τον ίδιο χρήστη των Windows, στο ίδιο μηχάνημα. Δεν γράφονται
ποτέ στο αποθετήριο και δεν εμφανίζονται στην οθόνη.

ΠΡΟΣΟΧΗ: ο κωδικός κρυπτογράφησης πρέπει να φυλαχθεί ΚΑΙ εκτός υπολογιστή (σε
διαχειριστή κωδικών ή σε χαρτί). Αν χαθεί ο υπολογιστής, το αρχείο DPAPI είναι
άχρηστο και τα αντίγραφα δεν ανοίγουν με τίποτα.

.EXAMPLE
.\init-secrets.ps1
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'lib\common.ps1')

Write-Host ''
Write-Host 'Ρύθμιση αντιγράφων ασφαλείας RentApp' -ForegroundColor Cyan
Write-Host '────────────────────────────────────'
Write-Host ''
Write-Host 'Το connection string της παραγωγικής βάσης (Neon → Connection Details).'
Write-Host 'Δεκτές και οι δύο μορφές:'
Write-Host '  postgresql://χρήστης:κωδικός@host/βάση?sslmode=require'
Write-Host '  Host=…;Port=5432;Database=…;Username=…;Password=…'
Write-Host ''

$connection = Read-Host 'Connection string' -AsSecureString

# Επαλήθευση πριν την αποθήκευση: καλύτερα να αποτύχει τώρα παρά στις 03:00.
$plain = [Runtime.InteropServices.Marshal]::PtrToStringBSTR(
    [Runtime.InteropServices.Marshal]::SecureStringToBSTR($connection))

$uri = Resolve-UnpooledUri (ConvertTo-PgUri $plain)

Write-Host ''
Write-Host 'Δοκιμή σύνδεσης…'
$probe = & (Get-PgTool 'psql') -d $uri -At -c 'select current_database()' 2>&1
if ($LASTEXITCODE -ne 0) {
    throw "Η σύνδεση απέτυχε, τίποτα δεν αποθηκεύτηκε: $probe"
}
Write-Host "Σύνδεση εντάξει — βάση '$probe'." -ForegroundColor Green

Write-Host ''
Write-Host 'Κωδικός κρυπτογράφησης των αντιγράφων.' -ForegroundColor Yellow
Write-Host 'Κράτησέ τον και εκτός υπολογιστή: χωρίς αυτόν κανένα αντίγραφο δεν επαναφέρεται.'
Write-Host ''

$pass1 = Read-Host 'Κωδικός' -AsSecureString
$pass2 = Read-Host 'Επανάληψη' -AsSecureString

$p1 = [Runtime.InteropServices.Marshal]::PtrToStringBSTR([Runtime.InteropServices.Marshal]::SecureStringToBSTR($pass1))
$p2 = [Runtime.InteropServices.Marshal]::PtrToStringBSTR([Runtime.InteropServices.Marshal]::SecureStringToBSTR($pass2))

if ($p1 -ne $p2)      { throw 'Οι δύο κωδικοί δεν ταιριάζουν — τίποτα δεν αποθηκεύτηκε.' }
if ($p1.Length -lt 12) { throw 'Ο κωδικός πρέπει να έχει τουλάχιστον 12 χαρακτήρες.' }

Save-Secret -Name 'connection' -Value $connection
Save-Secret -Name 'passphrase' -Value $pass1

Write-Host ''
Write-Host "Αποθηκεύτηκαν στο $env:LOCALAPPDATA\RentApp\backup\" -ForegroundColor Green
Write-Host ''
Write-Host 'Επόμενα βήματα:'
Write-Host '  .\backup-db.ps1        δοκιμαστική εκτέλεση τώρα'
Write-Host '  .\install-task.ps1     προγραμματισμός για κάθε μέρα στις 03:00'
Write-Host ''

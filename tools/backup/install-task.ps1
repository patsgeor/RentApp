<#
.SYNOPSIS
Καταχωρεί (ή αφαιρεί) την καθημερινή εργασία αντιγράφου στον Χρονοπρογραμματιστή.

.DESCRIPTION
Προεπιλογή: κάθε μέρα στις 03:00, όταν ο χρήστης είναι συνδεδεμένος. Αν ο
υπολογιστής ήταν κλειστός, η εργασία τρέχει μόλις ανοίξει (StartWhenAvailable).

Με -RunWhenLoggedOff η εργασία τρέχει και χωρίς συνδεδεμένο χρήστη, αλλά τα
Windows ζητούν τον κωδικό του λογαριασμού για να τον αποθηκεύσουν. Χωρίς αυτόν,
τα μυστικά DPAPI δεν αποκρυπτογραφούνται και η εργασία αποτυγχάνει.

.EXAMPLE
.\install-task.ps1

.EXAMPLE
.\install-task.ps1 -At 23:30 -RunWhenLoggedOff

.EXAMPLE
.\install-task.ps1 -Remove
#>
[CmdletBinding()]
param(
    [string]$TaskName = 'RentApp - Αντίγραφο βάσης',
    [datetime]$At = '03:00',
    [switch]$RunWhenLoggedOff,
    [switch]$Remove
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($Remove) {
    Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false
    Write-Host "Η εργασία '$TaskName' αφαιρέθηκε." -ForegroundColor Green
    return
}

$script = Join-Path $PSScriptRoot 'backup-db.ps1'
if (-not (Test-Path $script)) { throw "Δεν βρέθηκε το $script" }

$action = New-ScheduledTaskAction -Execute 'powershell.exe' `
    -Argument "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File `"$script`"" `
    -WorkingDirectory $PSScriptRoot

$trigger = New-ScheduledTaskTrigger -Daily -At $At

$settings = New-ScheduledTaskSettingsSet `
    -StartWhenAvailable `
    -DontStopIfGoingOnBatteries `
    -AllowStartIfOnBatteries `
    -MultipleInstances IgnoreNew `
    -ExecutionTimeLimit (New-TimeSpan -Hours 2)

if ($RunWhenLoggedOff) {
    $credential = Get-Credential -UserName "$env:USERDOMAIN\$env:USERNAME" `
        -Message 'Κωδικός Windows, ώστε η εργασία να τρέχει και χωρίς συνδεδεμένο χρήστη'

    Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger -Settings $settings `
        -User $credential.UserName `
        -Password $credential.GetNetworkCredential().Password `
        -RunLevel Limited -Force | Out-Null
}
else {
    $principal = New-ScheduledTaskPrincipal -UserId "$env:USERDOMAIN\$env:USERNAME" `
        -LogonType Interactive -RunLevel Limited

    Register-ScheduledTask -TaskName $TaskName -Action $action -Trigger $trigger `
        -Settings $settings -Principal $principal -Force | Out-Null
}

Write-Host ''
Write-Host "Η εργασία '$TaskName' καταχωρήθηκε για κάθε μέρα στις $($At.ToString('HH:mm'))." -ForegroundColor Green
Write-Host ''
Write-Host 'Χρήσιμα:'
Write-Host "  Start-ScheduledTask   -TaskName '$TaskName'     εκτέλεση τώρα"
Write-Host "  Get-ScheduledTaskInfo -TaskName '$TaskName'     τελευταίο αποτέλεσμα"
Write-Host "  .\install-task.ps1 -Remove                      αφαίρεση"
Write-Host ''

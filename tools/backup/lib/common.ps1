# Κοινές συναρτήσεις για τα scripts των αντιγράφων ασφαλείας.
# Γραμμένο για Windows PowerShell 5.1 — χωρίς εξαρτήσεις πέρα από το .NET Framework.

Set-StrictMode -Version Latest

. (Join-Path (Split-Path $PSScriptRoot -Parent) 'settings.ps1')

$Script:SecretsDir  = Join-Path $env:LOCALAPPDATA 'RentApp\backup'
$Script:Magic       = [Text.Encoding]::ASCII.GetBytes('RENTBAK1')   # 8 bytes
$Script:SaltSize    = 16
$Script:IvSize      = 16
$Script:MacSize     = 32

# ──────────────────────────────────────────────────────────── καταγραφή

function Write-Log {
    param(
        [Parameter(Mandatory)][string]$Message,
        [ValidateSet('INFO', 'WARN', 'ERROR')][string]$Level = 'INFO'
    )

    $line = '{0}  {1,-5}  {2}' -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), $Level, $Message

    switch ($Level) {
        'ERROR' { Write-Host $line -ForegroundColor Red }
        'WARN'  { Write-Host $line -ForegroundColor Yellow }
        default { Write-Host $line }
    }

    $logFile = Join-Path $Settings.BackupRoot 'backup.log'
    $dir = Split-Path $logFile -Parent
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }

    # Η καταγραφή δεν πρέπει ποτέ να ρίξει την εργασία: αν το αρχείο είναι
    # κλειδωμένο από άλλη εκτέλεση, προτιμάμε να χαθεί η γραμμή παρά το αντίγραφο.
    try { Add-Content -Path $logFile -Value $line -Encoding UTF8 } catch { }
}

# ──────────────────────────────────────────────────────────── μυστικά (DPAPI)

function Get-SecretPath {
    param([Parameter(Mandatory)][string]$Name)
    Join-Path $Script:SecretsDir "$Name.cred"
}

function Save-Secret {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][securestring]$Value
    )

    if (-not (Test-Path $Script:SecretsDir)) {
        New-Item -ItemType Directory -Path $Script:SecretsDir -Force | Out-Null
    }

    # ConvertFrom-SecureString: κρυπτογράφηση με DPAPI. Το αποτέλεσμα ανοίγει
    # ΜΟΝΟ από τον ίδιο χρήστη των Windows στο ίδιο μηχάνημα.
    Set-Content -Path (Get-SecretPath $Name) -Value (ConvertFrom-SecureString $Value) -Encoding ASCII
}

function Read-Secret {
    param([Parameter(Mandatory)][string]$Name)

    $path = Get-SecretPath $Name
    if (-not (Test-Path $path)) {
        throw "Δεν βρέθηκε το μυστικό '$Name'. Τρέξε πρώτα: .\init-secrets.ps1"
    }

    # Trim(): το Set-Content προσθέτει αλλαγή γραμμής στο τέλος και το
    # ConvertTo-SecureString απορρίπτει ό,τι δεν είναι καθαρό δεκαεξαδικό.
    $secure = ConvertTo-SecureString ((Get-Content $path -Raw).Trim())
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try { [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr) }
}

# ──────────────────────────────────────────────────────────── σύνδεση

<#
.SYNOPSIS
Μετατρέπει connection string σε μορφή libpq URI, που καταλαβαίνουν τα pg_dump/psql.

.DESCRIPTION
Δέχεται είτε URI (postgresql://…, όπως το δίνει η κονσόλα της Neon) είτε τη μορφή
του .NET (Host=…;Port=…;Database=…;Username=…;Password=…), που χρησιμοποιεί η
εφαρμογή. Επιστρέφει πάντα URI.
#>
function ConvertTo-PgUri {
    param([Parameter(Mandatory)][string]$ConnectionString)

    $value = $ConnectionString.Trim()

    if ($value -match '^postgres(ql)?://') { return $value }

    $parts = @{}
    foreach ($pair in $value.Split(';')) {
        if ([string]::IsNullOrWhiteSpace($pair)) { continue }
        $i = $pair.IndexOf('=')
        if ($i -lt 1) { continue }
        $parts[$pair.Substring(0, $i).Trim().ToLowerInvariant()] = $pair.Substring($i + 1).Trim()
    }

    foreach ($required in 'host', 'database', 'username') {
        if (-not $parts.ContainsKey($required)) {
            throw "Το connection string δεν περιέχει '$required'."
        }
    }

    $enc = { param($s) [Uri]::EscapeDataString($s) }

    $user = & $enc $parts['username']
    $pass = if ($parts.ContainsKey('password')) { ':' + (& $enc $parts['password']) } else { '' }
    $port = if ($parts.ContainsKey('port')) { ':' + $parts['port'] } else { '' }
    $db   = & $enc $parts['database']

    $query = @()
    if ($parts.ContainsKey('ssl mode')) { $query += 'sslmode=' + $parts['ssl mode'].ToLowerInvariant() }
    elseif ($parts.ContainsKey('sslmode')) { $query += 'sslmode=' + $parts['sslmode'].ToLowerInvariant() }

    $suffix = if ($query.Count) { '?' + ($query -join '&') } else { '' }

    "postgresql://$user$pass@$($parts['host'])$port/$db$suffix"
}

<#
.SYNOPSIS
Αντικαθιστά τον pooled host της Neon με τον απευθείας.

.DESCRIPTION
Η Neon προειδοποιεί ρητά να μη χρησιμοποιείται ο pooler για pg_dump: η σύνδεση
κόβεται σε μεγάλες μεταφορές και το αντίγραφο βγαίνει ελλιπές.
#>
function Resolve-UnpooledUri {
    param([Parameter(Mandatory)][string]$Uri)

    if ($Uri -notmatch '-pooler\.') { return $Uri }

    Write-Log 'Το connection string δείχνει στον pooler της Neon — χρησιμοποιείται ο απευθείας host.' -Level WARN
    $Uri -replace '-pooler\.', '.'
}

<#
.SYNOPSIS
Εκτελεί εξωτερικό πρόγραμμα και επιστρέφει έξοδο και κωδικό τερματισμού.

.DESCRIPTION
Τα εργαλεία της PostgreSQL γράφουν στο stderr και απλές πληροφορίες (π.χ.
«database does not exist, skipping»). Με $ErrorActionPreference = 'Stop', το
PowerShell μετατρέπει κάθε τέτοια γραμμή σε τερματιστικό σφάλμα και η εργασία
σταματά χωρίς λόγο. Εδώ η προτίμηση χαμηλώνει τοπικά και η κρίση για το τι είναι
σφάλμα γίνεται από τον κωδικό τερματισμού.
#>
function Invoke-Exe {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string[]]$Arguments
    )

    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $output = & $Path @Arguments 2>&1 | ForEach-Object { $_.ToString() }
        [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = @($output) }
    }
    finally { $ErrorActionPreference = $previous }
}

<#
.SYNOPSIS
Εκτελεί SQL μέσω psql, περνώντας το από αρχείο.

.DESCRIPTION
Το PowerShell 5.1 αφαιρεί τα διπλά εισαγωγικά όταν περνά ορίσματα σε native
εκτελέσιμο, οπότε ένα psql -c 'select * from "Tenants"' φτάνει ως Tenants χωρίς
εισαγωγικά — και η PostgreSQL το πεζοποιεί και δεν το βρίσκει. Με αρχείο δεν
υπάρχει τέτοιο πρόβλημα.
#>
function Invoke-PgSql {
    param(
        [Parameter(Mandatory)][string]$Uri,
        [Parameter(Mandatory)][string]$Sql,
        [switch]$Tuples
    )

    $file = Join-Path $env:TEMP ([IO.Path]::GetRandomFileName() + '.sql')

    # UTF-8 χωρίς BOM: ο psql διαβάζει το BOM ως δεδομένα και σκοντάφτει.
    [IO.File]::WriteAllText($file, $Sql, (New-Object Text.UTF8Encoding($false)))

    try {
        $env:PGCLIENTENCODING = 'UTF8'

        $arguments = @('-d', $Uri, '-v', 'ON_ERROR_STOP=1', '-f', $file)
        if ($Tuples) { $arguments = @('-At', '-F', '|') + $arguments }

        $result = Invoke-Exe -Path (Get-PgTool 'psql') -Arguments $arguments

        if ($result.ExitCode -ne 0) {
            throw "Το psql απέτυχε: $($result.Output -join '; ')"
        }

        # Οι ενημερωτικές γραμμές (NOTICE/WARNING) δεν είναι αποτελέσματα.
        $result.Output | Where-Object { $_ -notmatch '^(psql:|NOTICE:|WARNING:)' }
    }
    finally { Remove-Item $file -Force -ErrorAction SilentlyContinue }
}

function Get-PgTool {
    param([Parameter(Mandatory)][string]$Name)

    $path = Join-Path $Settings.PgBin "$Name.exe"
    if (-not (Test-Path $path)) {
        throw "Δεν βρέθηκε το $Name στο '$($Settings.PgBin)'. Διόρθωσε το PgBin στο settings.local.ps1."
    }
    $path
}

<#
.SYNOPSIS
Επιβεβαιώνει ότι ο client pg_dump δεν είναι παλαιότερος από τον διακομιστή.
#>
function Assert-PgVersion {
    param([Parameter(Mandatory)][string]$Uri)

    $clientRaw = (Invoke-Exe -Path (Get-PgTool 'pg_dump') -Arguments @('--version')).Output -join ' '
    if ($clientRaw -notmatch '(\d+)\.(\d+)') { throw "Δεν αναγνωρίστηκε η έκδοση του pg_dump: $clientRaw" }
    $clientMajor = [int]$Matches[1]

    $serverNum = Invoke-PgSql -Uri $Uri -Tuples -Sql 'show server_version_num'
    $serverMajor = [int]([int]($serverNum | Select-Object -First 1) / 10000)

    if ($clientMajor -lt $serverMajor) {
        throw "Ο client (pg_dump $clientMajor) είναι παλαιότερος από τον διακομιστή (PostgreSQL $serverMajor). Εγκατέστησε νεότερα εργαλεία."
    }

    Write-Log "PostgreSQL διακομιστής: $serverMajor · pg_dump: $clientMajor"
}

# ──────────────────────────────────────────────────────────── κρυπτογράφηση
#
# Μορφή αρχείου:
#   [8  bytes]  μαγικό 'RENTBAK1'
#   [16 bytes]  salt του PBKDF2
#   [16 bytes]  IV του AES
#   [   …    ]  κρυπτογράφημα (AES-256-CBC, PKCS7)
#   [32 bytes]  HMAC-SHA256 όλων των προηγούμενων (encrypt-then-MAC)
#
# Δύο ανεξάρτητα κλειδιά παράγονται από τον ίδιο κωδικό: ένα για την
# κρυπτογράφηση και ένα για το HMAC.

function Get-DerivedKeys {
    param(
        [Parameter(Mandatory)][string]$Passphrase,
        [Parameter(Mandatory)][byte[]]$Salt
    )

    $kdf = New-Object Security.Cryptography.Rfc2898DeriveBytes(
        $Passphrase, $Salt, $Settings.KdfIterations, [Security.Cryptography.HashAlgorithmName]::SHA256)
    try {
        @{ Aes = $kdf.GetBytes(32); Mac = $kdf.GetBytes(32) }
    }
    finally { $kdf.Dispose() }
}

function Add-FileHmac {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][byte[]]$MacKey
    )

    $hmac = New-Object Security.Cryptography.HMACSHA256(, $MacKey)
    try {
        $stream = [IO.File]::OpenRead($Path)
        try { $tag = $hmac.ComputeHash($stream) } finally { $stream.Dispose() }
    }
    finally { $hmac.Dispose() }

    $out = [IO.File]::Open($Path, [IO.FileMode]::Append)
    try { $out.Write($tag, 0, $tag.Length) } finally { $out.Dispose() }
}

function Test-FileHmac {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][byte[]]$MacKey
    )

    $total = (Get-Item $Path).Length
    $bodyLength = $total - $Script:MacSize
    if ($bodyLength -le ($Script:Magic.Length + $Script:SaltSize + $Script:IvSize)) { return $false }

    $stream = [IO.File]::OpenRead($Path)
    try {
        $hmac = New-Object Security.Cryptography.HMACSHA256(, $MacKey)
        try {
            $buffer = New-Object byte[] 81920
            $remaining = $bodyLength
            while ($remaining -gt 0) {
                $read = $stream.Read($buffer, 0, [Math]::Min($buffer.Length, $remaining))
                if ($read -le 0) { break }
                [void]$hmac.TransformBlock($buffer, 0, $read, $null, 0)
                $remaining -= $read
            }
            [void]$hmac.TransformFinalBlock(@(), 0, 0)
            $expected = $hmac.Hash
        }
        finally { $hmac.Dispose() }

        $actual = New-Object byte[] $Script:MacSize
        [void]$stream.Read($actual, 0, $Script:MacSize)
    }
    finally { $stream.Dispose() }

    # Σύγκριση σταθερού χρόνου.
    $diff = 0
    for ($i = 0; $i -lt $Script:MacSize; $i++) { $diff = $diff -bor ($expected[$i] -bxor $actual[$i]) }
    $diff -eq 0
}

function Protect-BackupFile {
    param(
        [Parameter(Mandatory)][string]$InputPath,
        [Parameter(Mandatory)][string]$OutputPath,
        [Parameter(Mandatory)][string]$Passphrase
    )

    $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $salt = New-Object byte[] $Script:SaltSize
        $iv   = New-Object byte[] $Script:IvSize
        $rng.GetBytes($salt)
        $rng.GetBytes($iv)
    }
    finally { $rng.Dispose() }

    $keys = Get-DerivedKeys -Passphrase $Passphrase -Salt $salt

    $aes = [Security.Cryptography.Aes]::Create()
    $aes.KeySize = 256
    $aes.Mode    = [Security.Cryptography.CipherMode]::CBC
    $aes.Padding = [Security.Cryptography.PaddingMode]::PKCS7
    $aes.Key     = $keys.Aes
    $aes.IV      = $iv

    try {
        $out = [IO.File]::Create($OutputPath)
        try {
            $out.Write($Script:Magic, 0, $Script:Magic.Length)
            $out.Write($salt, 0, $salt.Length)
            $out.Write($iv, 0, $iv.Length)

            $encryptor = $aes.CreateEncryptor()
            try {
                $crypto = New-Object Security.Cryptography.CryptoStream($out, $encryptor, [Security.Cryptography.CryptoStreamMode]::Write)
                try {
                    $in = [IO.File]::OpenRead($InputPath)
                    try { $in.CopyTo($crypto) } finally { $in.Dispose() }
                    $crypto.FlushFinalBlock()
                }
                finally { $crypto.Dispose() }
            }
            finally { $encryptor.Dispose() }
        }
        finally { $out.Dispose() }
    }
    finally { $aes.Dispose() }

    Add-FileHmac -Path $OutputPath -MacKey $keys.Mac
}

function Unprotect-BackupFile {
    param(
        [Parameter(Mandatory)][string]$InputPath,
        [Parameter(Mandatory)][string]$OutputPath,
        [Parameter(Mandatory)][string]$Passphrase
    )

    $header = New-Object byte[] ($Script:Magic.Length + $Script:SaltSize + $Script:IvSize)
    $stream = [IO.File]::OpenRead($InputPath)
    try { [void]$stream.Read($header, 0, $header.Length) } finally { $stream.Dispose() }

    for ($i = 0; $i -lt $Script:Magic.Length; $i++) {
        if ($header[$i] -ne $Script:Magic[$i]) {
            throw "Το '$InputPath' δεν είναι αρχείο αντιγράφου RentApp."
        }
    }

    $salt = $header[$Script:Magic.Length..($Script:Magic.Length + $Script:SaltSize - 1)]
    $iv   = $header[($Script:Magic.Length + $Script:SaltSize)..($header.Length - 1)]

    $keys = Get-DerivedKeys -Passphrase $Passphrase -Salt $salt

    # Πρώτα ο έλεγχος γνησιότητας: αν ο κωδικός είναι λάθος ή το αρχείο έχει
    # αλλοιωθεί, το μαθαίνουμε εδώ και όχι από σκουπίδια στην έξοδο.
    if (-not (Test-FileHmac -Path $InputPath -MacKey $keys.Mac)) {
        throw 'Λάθος κωδικός ή αλλοιωμένο αρχείο: ο έλεγχος ακεραιότητας (HMAC) απέτυχε.'
    }

    $aes = [Security.Cryptography.Aes]::Create()
    $aes.KeySize = 256
    $aes.Mode    = [Security.Cryptography.CipherMode]::CBC
    $aes.Padding = [Security.Cryptography.PaddingMode]::PKCS7
    $aes.Key     = $keys.Aes
    $aes.IV      = $iv

    try {
        $in = [IO.File]::OpenRead($InputPath)
        try {
            $in.Position = $header.Length
            $cipherLength = $in.Length - $header.Length - $Script:MacSize

            $out = [IO.File]::Create($OutputPath)
            try {
                $decryptor = $aes.CreateDecryptor()
                try {
                    $crypto = New-Object Security.Cryptography.CryptoStream($out, $decryptor, [Security.Cryptography.CryptoStreamMode]::Write)
                    try {
                        $buffer = New-Object byte[] 81920
                        $remaining = $cipherLength
                        while ($remaining -gt 0) {
                            $read = $in.Read($buffer, 0, [Math]::Min($buffer.Length, $remaining))
                            if ($read -le 0) { break }
                            $crypto.Write($buffer, 0, $read)
                            $remaining -= $read
                        }
                        $crypto.FlushFinalBlock()
                    }
                    finally { $crypto.Dispose() }
                }
                finally { $decryptor.Dispose() }
            }
            finally { $out.Dispose() }
        }
        finally { $in.Dispose() }
    }
    finally { $aes.Dispose() }
}

# ──────────────────────────────────────────────────────────── διατήρηση

<#
.SYNOPSIS
Επιστρέφει τα αρχεία που πρέπει να διαγραφούν, με πολιτική GFS.

.DESCRIPTION
Κρατά τα N πιο πρόσφατα ημερήσια, το νεότερο κάθε μίας από τις τελευταίες
εβδομάδες και το νεότερο κάθε ενός από τους τελευταίους μήνες. Η ημερομηνία
διαβάζεται από το όνομα του αρχείου και όχι από τον χρόνο του συστήματος, ώστε
η αντιγραφή σε άλλον φάκελο να μην αλλάζει την πολιτική.
#>
function Get-ExpiredBackups {
    param([Parameter(Mandatory)][System.IO.FileInfo[]]$Files)

    $parsed = @()
    foreach ($file in $Files) {
        if ($file.Name -match 'rentapp-(\d{8})-(\d{4})\.dump\.enc$') {
            $stamp = [datetime]::ParseExact("$($Matches[1])$($Matches[2])", 'yyyyMMddHHmm', $null)
            $parsed += [pscustomobject]@{ File = $file; Stamp = $stamp }
        }
    }

    if (-not $parsed) { return @() }

    $parsed = $parsed | Sort-Object Stamp -Descending
    $keep = New-Object Collections.Generic.HashSet[string]

    foreach ($item in ($parsed | Select-Object -First $Settings.KeepDaily)) {
        [void]$keep.Add($item.File.FullName)
    }

    $cal = [Globalization.CultureInfo]::InvariantCulture.Calendar
    $byWeek = $parsed | Group-Object { '{0}-{1}' -f $_.Stamp.Year, $cal.GetWeekOfYear($_.Stamp, [Globalization.CalendarWeekRule]::FirstFourDayWeek, [DayOfWeek]::Monday) }
    foreach ($group in ($byWeek | Select-Object -First $Settings.KeepWeekly)) {
        [void]$keep.Add((($group.Group | Sort-Object Stamp -Descending)[0]).File.FullName)
    }

    $byMonth = $parsed | Group-Object { $_.Stamp.ToString('yyyy-MM') }
    foreach ($group in ($byMonth | Select-Object -First $Settings.KeepMonthly)) {
        [void]$keep.Add((($group.Group | Sort-Object Stamp -Descending)[0]).File.FullName)
    }

    $parsed | Where-Object { -not $keep.Contains($_.File.FullName) } | ForEach-Object { $_.File }
}

function Remove-ExpiredBackups {
    param([Parameter(Mandatory)][string]$Folder)

    if (-not (Test-Path $Folder)) { return }

    $files = @(Get-ChildItem -Path $Folder -Filter 'rentapp-*.dump.enc' -File)
    if (-not $files) { return }

    foreach ($expired in (Get-ExpiredBackups -Files $files)) {
        Remove-Item $expired.FullName -Force
        Write-Log "Διαγράφηκε παλαιό αντίγραφο: $($expired.Name)"
    }
}

# Προεπιλεγμένες ρυθμίσεις των αντιγράφων ασφαλείας.
#
# ΚΑΜΙΑ ΜΥΣΤΙΚΗ ΤΙΜΗ εδώ: το αρχείο μπαίνει στο αποθετήριο. Το connection string
# και ο κωδικός κρυπτογράφησης αποθηκεύονται ξεχωριστά με το init-secrets.ps1.
#
# Για τοπικές παρακάμψεις φτιάξε settings.local.ps1 δίπλα σε αυτό (αγνοείται από
# το git) και όρισε ξανά όποια μεταβλητή θέλεις.

$Script:Settings = @{

    # Πρωτεύων φάκελος αντιγράφων (στον δίσκο του μηχανήματος).
    BackupRoot = Join-Path $env:USERPROFILE 'RentApp-Backups'

    # Δευτερεύων φάκελος, εκτός μηχανήματος. Το OneDrive τον συγχρονίζει μόνο του.
    # Κενό = χωρίς δεύτερο αντίγραφο.
    MirrorRoot = if ($env:OneDrive) { Join-Path $env:OneDrive 'RentApp-Backups' } else { '' }

    # Εργαλεία PostgreSQL. Η έκδοση πρέπει να είναι ίδια ή νεότερη από του διακομιστή.
    PgBin = 'C:\Program Files\PostgreSQL\18\bin'

    # Πολιτική διατήρησης (grandfather-father-son).
    KeepDaily   = 7
    KeepWeekly  = 4
    KeepMonthly = 6

    # Επαναλήψεις PBKDF2 για την παραγωγή του κλειδιού από τον κωδικό.
    # Όσο μεγαλύτερος ο αριθμός, τόσο ακριβότερη η δοκιμή κωδικών με τη βία.
    KdfIterations = 200000

    # Τοπική βάση για τις ασκήσεις επαναφοράς (verify-backup.ps1). Διαγράφεται
    # και ξαναδημιουργείται σε κάθε εκτέλεση — μη βάλεις εδώ βάση με δεδομένα.
    DrillDatabase = 'RentDb_restoretest'

    # Σύνδεση με τον τοπικό διακομιστή PostgreSQL, για τις ασκήσεις επαναφοράς.
    LocalHost     = 'localhost'
    LocalPort     = 5432
    LocalUser     = 'postgres'
}

$localOverrides = Join-Path $PSScriptRoot 'settings.local.ps1'
if (Test-Path $localOverrides) { . $localOverrides }

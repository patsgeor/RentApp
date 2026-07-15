namespace API.Entities;

public static class Enums
{
    public enum CustomerType { Person = 0, Company = 1 }
    public enum AcquisitionType { Purchase = 0, Leasing = 1 }
    public enum RateUnit { PerHour = 0, PerDay = 1, PerMonth = 2, Sale = 3 }
    public enum AssetStatus { Available = 0, Rented = 1, UnderMaintenance = 2, Damaged = 3 }
    public enum RentalStatus { Pending = 0, Active = 1, Completed = 2, Cancelled = 3 }
    public enum PaymentMethod { Cash = 0, Card = 1, BankTransfer = 2 }
    public enum TransactionType { Income = 0, Expense = 1 }
    public enum SubscriptionStatus { Trial = 0, Active = 1, Suspended = 2, Cancelled = 3 }
    public enum FieldDataType { Text = 0, Number = 1, Boolean = 2, Date = 3, DateTime = 4 }

    // Κατάσταση περιοδικής οφειλής
    public enum InstallmentStatus
    {
        Pending       = 0,  // αναμένεται, δεν έχει έρθει ακόμα η ημερομηνία
        PartiallyPaid = 1,  // μερική εξόφληση
        Paid          = 2,  // πλήρης εξόφληση
        Overdue       = 3,  // εκπρόθεσμη (DueDate < today && !Paid)
        Cancelled     = 4   // ακυρώθηκε
    }

    // Κατάσταση αντιστοίχισης πληρωμής
    public enum PaymentMatchStatus
    {
        Unmatched       = 0,  // δεν βρέθηκε συμβόλαιο — χειροκίνητη αντιστοίχιση
        AutoMatched     = 1,  // αυτόματη αντιστοίχιση μέσω ReferenceCode
        ManuallyMatched = 2   // χειροκίνητη αντιστοίχιση από χρήστη
    }

    // Συχνότητα δημιουργίας οφειλών
     public enum InstallmentFrequency { Monthly =0, Weekly =1, Quarterly =2, Yearly =3, OneTime =4 }
}
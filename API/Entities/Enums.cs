namespace API.Entities;

public static class Enums
{
    public enum CustomerType { Person, Company }
    public enum AcquisitionType { Purchase, Leasing }
    public enum AssetStatus { Available, Rented, UnderMaintenance, Damaged }
    public enum RentalStatus { Pending, Active, Completed, Cancelled }
    public enum PaymentMethod { Cash, Card, BankTransfer }
    public enum TransactionType { Income, Expense }
    public enum SubscriptionStatus { Trial, Active, Suspended, Cancelled }
 }

namespace API.Entities;

public static class Enums
{
    public enum CustomerType { Person =0 , Company =1 }
    public enum AcquisitionType { Purchase =0, Leasing =1 }
    public enum AssetStatus { Available =0, Rented =1, UnderMaintenance =2, Damaged =3 }
    public enum RentalStatus { Pending =0, Active =1, Completed =2, Cancelled =3 }
    public enum PaymentMethod { Cash =0, Card =1, BankTransfer =2 }
    public enum TransactionType { Income =0, Expense =1 }
    public enum SubscriptionStatus { Trial=0, Active =1, Suspended =2, Cancelled =3 }
    public enum FieldDataType{Text =0, Number =1, Boolean =2, Date =3, DateTime =4}
 }

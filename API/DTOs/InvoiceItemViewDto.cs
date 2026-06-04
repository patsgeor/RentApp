using System;

namespace API.DTOs;

public class InvoiceItemViewDto
{
    
    public long Id { get; set; }
        
    // Βασικά στοιχεία γραμμής
    public required string Description { get; set; } 
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; } // Quantity * UnitPrice

    // Σχετικά Λεξικά (Flattened - φέρνουμε μόνο τα Ονόματα, όχι τα Ids)
    public required string CategoryName { get; set; }          // Π.χ. "Υπολογιστές"
    public required string CategoryLog { get; set; }          // Π.χ. "14.01" (αν το χρειάζεται το λογιστήριο)
    public required string UnitMetricDescription { get; set; }   // Π.χ. "Τεμάχια"
    public required string DepreciationMethodName { get; set; } // Π.χ. "Σταθερή"
    public required string ValuationMethodName { get; set; }    // Π.χ. "Αξια κτήσης"
}

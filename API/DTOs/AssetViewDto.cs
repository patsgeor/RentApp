using System;
using System.ComponentModel.DataAnnotations;
using API.Entities;
using Microsoft.EntityFrameworkCore;

namespace API.DTOs;

public class RentViewDto
{
    public long Id { get; set; }
    public string Description {get; set;}="";
    public string LogCategory {get; set;}="";
    public  string? Notes { get; set; }
    public  string CreatedByMemberName { get; set; } ="";
    public  string MonadaName { get; set; }=""; 
    public long InvoiceItemId { get; set; }
    public string? SerialNumber { get; set; }

    public decimal InitialValue { get; set; }//τιμή κτήσης
    public decimal CurrentValue { get; set; } //τρέχουσα αξία, υπολογίζεται με βάση την μέθοδο αποσβέσεων
    public decimal ResidualValue { get; set; } //υπολειμματική αξία, default 0.01
    public DateTime AcquiredDate { get; set; }//ημερομηνία κτήσης
    public int UsefulLifeYears { get; set; } //ωφέλιμη ζωή σε έτη

    public bool IsLocked { get; set; } = false; //αν το Rent είναι κλειδωμένο για επεξεργασία
   

    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }


   

}

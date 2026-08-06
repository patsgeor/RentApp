using API.DTOs.Report;
using ClosedXML.Excel;

namespace API.Services.Reports;

/// <summary>
/// Μετατρέπει τα δεδομένα μιας αναφοράς σε workbook. Καμία λογική ερωτημάτων εδώ:
/// αν χρειαστεί αλλαγή σε streaming βιβλιοθήκη λόγω όγκου, αντικαθίσταται μόνο
/// αυτή η κλάση.
/// </summary>
public static class ExcelWorkbookBuilder
{
    private const string MoneyFormat = "#,##0.00 €";
    private const string DateFormat  = "dd/mm/yyyy";

    public static XLWorkbook Build(ReportDataDto data)
    {
        var wb = new XLWorkbook();

        if (data.Kpi is not null)             AddKpiSheet(wb, data);
        if (data.Income is not null)          AddPaymentsSheet(wb, "Έσοδα", data.Income);
        if (data.Expenses is not null)        AddPaymentsSheet(wb, "Έξοδα", data.Expenses);
        if (data.Contracts is not null)       AddContractsSheet(wb, data.Contracts);
        if (data.Assets is not null)          AddAssetsSheet(wb, data.Assets);
        if (data.AssetAttributes is { Count: > 0 }) AddAssetAttributesSheet(wb, data.AssetAttributes);
        if (data.Installments is not null)    AddInstallmentsSheet(wb, data.Installments);
        if (data.Customers is not null)       AddCustomersSheet(wb, data.Customers);

        // Ένα workbook χωρίς φύλλα δεν ανοίγει στο Excel.
        if (!wb.Worksheets.Any())
            wb.AddWorksheet("Κενή Αναφορά").Cell(1, 1).Value = "Δεν βρέθηκαν δεδομένα για την επιλεγμένη περίοδο.";

        return wb;
    }

    // ── Sheets ──────────────────────────────────────────────────────────────

    private static void AddKpiSheet(XLWorkbook wb, ReportDataDto data)
    {
        var k = data.Kpi!;
        var ws = wb.AddWorksheet("Σύνοψη");

        ws.Cell(1, 1).Value = "Αναφορά Περιόδου";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;

        // Το άνω όριο είναι αποκλειστικό εσωτερικά· εμφανίζεται η τελευταία ημέρα.
        ws.Cell(2, 1).Value = $"Από {data.From:dd/MM/yyyy} έως {data.To.AddDays(-1):dd/MM/yyyy}";
        ws.Cell(3, 1).Value = data.AssetMode == AssetPeriodMode.Registered
            ? "Πάγια: όσα καταχωρήθηκαν στην περίοδο"
            : "Πάγια: όσα ήταν ενοικιασμένα στην περίοδο";
        ws.Cell(3, 1).Style.Font.Italic = true;

        var row = 5;
        void Section(string title)
        {
            ws.Cell(row, 1).Value = title;
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#DCE6F1");
            ws.Cell(row, 2).Style.Fill.BackgroundColor = XLColor.FromHtml("#DCE6F1");
            row++;
        }
        void Money(string label, decimal value)
        {
            ws.Cell(row, 1).Value = label;
            ws.Cell(row, 2).Value = value;
            ws.Cell(row, 2).Style.NumberFormat.Format = MoneyFormat;
            row++;
        }
        void Count(string label, int value)
        {
            ws.Cell(row, 1).Value = label;
            ws.Cell(row, 2).Value = value;
            row++;
        }

        Section("Οικονομικά περιόδου");
        Money("Έσοδα", k.Income);
        Money("Έξοδα", k.Expenses);
        Money("Καθαρό αποτέλεσμα", k.Net);
        Money("Εισπράχθηκε έναντι δόσεων", k.CollectedInPeriod);
        row++;

        Section("Δραστηριότητα περιόδου");
        Count("Νέα συμβόλαια (καταχώρηση)", k.ContractsCreated);
        Count("Συμβόλαια που ξεκίνησαν", k.ContractsStarted);
        Count("Συμβόλαια που έληξαν", k.ContractsEnded);
        Count("Πάγια που καταχωρήθηκαν", k.AssetsRegistered);
        Count("Νέοι πελάτες", k.CustomersRegistered);
        Count("Δόσεις με λήξη στην περίοδο", k.InstallmentsDue);
        Money("Ποσό δόσεων περιόδου", k.InstallmentsDueAmount);
        row++;

        Section("Τρέχουσα κατάσταση");
        Money("Συνολικά ανεξόφλητα", k.OutstandingNow);
        Count("Ληξιπρόθεσμες δόσεις", k.OverdueCountNow);
        Money("Ληξιπρόθεσμο ποσό", k.OverdueAmountNow);
        Count("Ενεργά συμβόλαια", k.ActiveContractsNow);
        Count("Σύνολο παγίων", k.TotalAssets);

        ws.Cell(row + 1, 1).Value = "Τα ακυρωμένα συμβόλαια εξαιρούνται από όλα τα μεγέθη.";
        ws.Cell(row + 1, 1).Style.Font.Italic = true;
        ws.Cell(row + 1, 1).Style.Font.FontColor = XLColor.Gray;

        ws.Column(1).Width = 34;
        ws.Column(2).Width = 18;
    }

    private static void AddPaymentsSheet(XLWorkbook wb, string name, List<PaymentRowDto> rows)
    {
        var ws = Sheet(wb, name, ["Ημερομηνία", "Ποσό", "Τρόπος", "Πελάτης", "Παραστατικό", "Περιγραφή", "Σημειώσεις", "Αδιάθετο"]);

        var r = 2;
        foreach (var p in rows)
        {
            ws.Cell(r, 1).Value = p.PaymentDate;
            ws.Cell(r, 1).Style.DateFormat.Format = DateFormat;
            ws.Cell(r, 2).Value = p.Amount;
            ws.Cell(r, 2).Style.NumberFormat.Format = MoneyFormat;
            ws.Cell(r, 3).Value = p.PaymentMethod;
            ws.Cell(r, 4).Value = p.CustomerName ?? "";
            ws.Cell(r, 5).Value = p.ReferenceCode ?? "";
            ws.Cell(r, 6).Value = p.Description ?? "";
            ws.Cell(r, 7).Value = p.Notes ?? "";
            ws.Cell(r, 8).Value = p.UnallocatedAmount;
            ws.Cell(r, 8).Style.NumberFormat.Format = MoneyFormat;
            r++;
        }

        if (rows.Count > 0) Total(ws, r, 2, rows.Sum(x => x.Amount));
        Finish(ws);
    }

    private static void AddContractsSheet(XLWorkbook wb, List<ContractRowDto> rows)
    {
        var ws = Sheet(wb, "Συμβόλαια", ["Κωδικός", "Πελάτης", "Έναρξη", "Λήξη", "Σύνολο", "Εισπραχθέν", "Υπόλοιπο", "Κατάσταση", "Καταχώρηση"]);

        var r = 2;
        foreach (var c in rows)
        {
            ws.Cell(r, 1).Value = c.ReferenceCode ?? "";
            ws.Cell(r, 2).Value = c.CustomerName;
            ws.Cell(r, 3).Value = c.StartDate; ws.Cell(r, 3).Style.DateFormat.Format = DateFormat;
            ws.Cell(r, 4).Value = c.EndDate;   ws.Cell(r, 4).Style.DateFormat.Format = DateFormat;
            ws.Cell(r, 5).Value = c.TotalAmount;       ws.Cell(r, 5).Style.NumberFormat.Format = MoneyFormat;
            ws.Cell(r, 6).Value = c.PaidAmount;        ws.Cell(r, 6).Style.NumberFormat.Format = MoneyFormat;
            ws.Cell(r, 7).Value = c.OutstandingAmount; ws.Cell(r, 7).Style.NumberFormat.Format = MoneyFormat;
            ws.Cell(r, 8).Value = c.Status;
            ws.Cell(r, 9).Value = c.CreatedAt; ws.Cell(r, 9).Style.DateFormat.Format = DateFormat;
            r++;
        }

        if (rows.Count > 0)
        {
            Total(ws, r, 5, rows.Sum(x => x.TotalAmount));
            Total(ws, r, 6, rows.Sum(x => x.PaidAmount), label: false);
            Total(ws, r, 7, rows.Sum(x => x.OutstandingAmount), label: false);
        }
        Finish(ws);
    }

    private static void AddAssetsSheet(XLWorkbook wb, List<AssetRowDto> rows)
    {
        var ws = Sheet(wb, "Πάγια", ["Όνομα", "Κατηγορία", "Κόστος", "Κατάσταση", "Ημ. Καταχώρησης"]);

        var r = 2;
        foreach (var a in rows)
        {
            ws.Cell(r, 1).Value = a.Name;
            ws.Cell(r, 2).Value = a.AssetTypeName;
            ws.Cell(r, 3).Value = a.Cost; ws.Cell(r, 3).Style.NumberFormat.Format = MoneyFormat;
            ws.Cell(r, 4).Value = a.Status;
            ws.Cell(r, 5).Value = a.CreatedAt; ws.Cell(r, 5).Style.DateFormat.Format = DateFormat;
            r++;
        }

        if (rows.Count > 0) Total(ws, r, 3, rows.Sum(x => x.Cost));
        Finish(ws);
    }

    private static void AddAssetAttributesSheet(XLWorkbook wb, List<AssetAttributeRowDto> rows)
    {
        var ws = Sheet(wb, "Πάγια-Χαρακτηριστικά", ["Πάγιο", "Κατηγορία", "Πεδίο", "Τιμή"]);

        var r = 2;
        foreach (var a in rows)
        {
            ws.Cell(r, 1).Value = a.AssetName;
            ws.Cell(r, 2).Value = a.AssetTypeName;
            ws.Cell(r, 3).Value = a.Field;
            ws.Cell(r, 4).Value = a.Value;
            r++;
        }
        Finish(ws);
    }

    private static void AddInstallmentsSheet(XLWorkbook wb, List<InstallmentRowDto> rows)
    {
        var ws = Sheet(wb, "Δόσεις", ["Συμβόλαιο", "Πελάτης", "Α/Α", "Λήξη", "Ποσό", "Εισπραχθέν", "Υπόλοιπο", "Κατάσταση"]);

        var r = 2;
        foreach (var i in rows)
        {
            ws.Cell(r, 1).Value = i.ContractReferenceCode ?? "";
            ws.Cell(r, 2).Value = i.CustomerName;
            ws.Cell(r, 3).Value = i.InstallmentNumber;
            ws.Cell(r, 4).Value = i.DueDate; ws.Cell(r, 4).Style.DateFormat.Format = DateFormat;
            ws.Cell(r, 5).Value = i.TotalAmount;       ws.Cell(r, 5).Style.NumberFormat.Format = MoneyFormat;
            ws.Cell(r, 6).Value = i.AllocatedAmount;   ws.Cell(r, 6).Style.NumberFormat.Format = MoneyFormat;
            ws.Cell(r, 7).Value = i.OutstandingAmount; ws.Cell(r, 7).Style.NumberFormat.Format = MoneyFormat;
            ws.Cell(r, 8).Value = i.Status;
            r++;
        }

        if (rows.Count > 0)
        {
            Total(ws, r, 5, rows.Sum(x => x.TotalAmount));
            Total(ws, r, 6, rows.Sum(x => x.AllocatedAmount), label: false);
            Total(ws, r, 7, rows.Sum(x => x.OutstandingAmount), label: false);
        }
        Finish(ws);
    }

    private static void AddCustomersSheet(XLWorkbook wb, List<CustomerRowDto> rows)
    {
        var ws = Sheet(wb, "Πελάτες", ["Επωνυμία", "Τύπος", "ΑΦΜ", "ΔΟΥ", "Διεύθυνση", "Ημ. Καταχώρησης"]);

        var r = 2;
        foreach (var c in rows)
        {
            ws.Cell(r, 1).Value = c.Name;
            ws.Cell(r, 2).Value = c.Type;
            ws.Cell(r, 3).Value = c.Afm ?? "";
            ws.Cell(r, 4).Value = c.Dou ?? "";
            ws.Cell(r, 5).Value = c.Address ?? "";
            ws.Cell(r, 6).Value = c.CreatedAt; ws.Cell(r, 6).Style.DateFormat.Format = DateFormat;
            r++;
        }
        Finish(ws);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static IXLWorksheet Sheet(XLWorkbook wb, string name, string[] headers)
    {
        var ws = wb.AddWorksheet(name);

        for (var i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#DCE6F1");
        }

        ws.SheetView.FreezeRows(1);
        return ws;
    }

    private static void Total(IXLWorksheet ws, int row, int col, decimal value, bool label = true)
    {
        if (label)
        {
            ws.Cell(row, col - 1).Value = "Σύνολο";
            ws.Cell(row, col - 1).Style.Font.Bold = true;
        }
        var cell = ws.Cell(row, col);
        cell.Value = value;
        cell.Style.Font.Bold = true;
        cell.Style.NumberFormat.Format = MoneyFormat;
        cell.Style.Border.TopBorder = XLBorderStyleValues.Thin;
    }

    private static void Finish(IXLWorksheet ws) => ws.Columns().AdjustToContents();
}

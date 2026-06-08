using System;
using System.Text.Json;
using API.Data.Contexts;
using API.Entities;
using Bogus;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace API.Data;

public class DbInitializer
{

    
    public static async Task InitializeAsync(AppDbContext context, UserManager<AppUser> userManager)
    {
        // 1. Δημιουργία της βάσης αν δεν υπάρχει
        await context.Database.EnsureCreatedAsync();

        // 2. Έλεγχος αν υπάρχουν ήδη δεδομένα
        if (await context.Tenants
                        .IgnoreQueryFilters()
                        .AnyAsync())
        {
            return; // Η βάση έχει ήδη αρχικοποιηθεί
        }

        // ==========================================
        // 3. ΒΑΣΙΚΑ ΔΕΔΟΜΕΝΑ (Λίστες για Ρεαλισμό)
        // ==========================================
        var greekCities = new[] { 
            "ΑΘΗΝΑ", "ΘΕΣΣΑΛΟΝΙΚΗ", "ΠΑΤΡΑ", "ΗΡΑΚΛΕΙΟ", "ΛΑΡΙΣΑ", 
            "ΒΟΛΟΣ", "ΙΩΑΝΝΙΝΑ", "ΤΡΙΚΑΛΑ", "ΧΑΛΚΙΔΑ", "ΣΕΡΡΕΣ", "ΑΛΕΞΑΝΔΡΟΥΠΟΛΗ" 
        };

        var greekDous = new[] { "Α' ΑΘΗΝΩΝ", "Δ' ΘΕΣΣΑΛΟΝΙΚΗΣ", "ΧΑΛΑΝΔΡΙΟΥ", "Α' ΠΕΡΙΣΤΕΡΙΟΥ", "ΓΛΥΦΑΔΑΣ", "ΗΡΑΚΛΕΙΟΥ" };
        var authors = new[] { "Νίκος Καζαντζάκης", "Οδυσσέας Ελύτης", "Γιώργος Σεφέρης", "Αλέξανδρος Παπαδιαμάντης", "Πηνελόπη Δέλτα" };
        var machineBrands = new[] { "Caterpillar", "Bosch", "Makita", "DeWalt", "Komatsu" };

        // Ρύθμιση του Bogus για ελληνικά δεδομένα (όπου υποστηρίζεται)
        Randomizer.Seed = new Random(8675309); // Σταθερό seed για επαναληψιμότητα στα tests

        // ==========================================
        // 4. ΔΗΜΙΟΥΡΓΙΑ TENANTS (Εταιρείες)
        // ==========================================
        string bookUserId="" ;
        string machineUserId="";
        string warehouseUserId="";
        var tenantBook = Guid.NewGuid(); 
        var tenantMachine = Guid.NewGuid(); 
        var tenantWarehouse = Guid.NewGuid(); 

        var tenants = new List<Tenant>
        {
            new Tenant
            {
                Id = tenantBook,
                Name = "Hellenic Rentals Book A.E.",
                VatNumber = "099999999",
                ContactInfo = "Λεωφόρος Κηφισίας 150, Αθήνα",
                SubscriptionStatus = Enums.SubscriptionStatus.Active
            },

            new Tenant
            {
                Id = tenantMachine,
                Name = "Rentals Machine A.E.",
                VatNumber = "099929999",
                ContactInfo = "Λεωφόρος Αθηνων 150, Αθήνα",
                SubscriptionStatus = Enums.SubscriptionStatus.Active
            },

            new Tenant
            {
                Id = tenantWarehouse,
                Name = "Rentals Warehouse A.E.",
                VatNumber = "099939999",
                ContactInfo = "Κηφισίας 150, Αθήνα",
                SubscriptionStatus = Enums.SubscriptionStatus.Active
            }
        };

        await context.Tenants.AddRangeAsync(tenants);
        await context.SaveChangesAsync();

        // ==========================================
        // 5. ΔΗΜΙΟΥΡΓΙΑ USERS (Χρήστες Συστήματος)
        // ==========================================
        var users = new[]
        {
            new AppUser
            {
                UserName = "book.admin@rentals.gr",
                Email = "book.admin@rentals.gr",
                DisplayName = "Book Admin",
                TenantId = tenantBook,
                IsActive = true,
                EmailConfirmed = true
            },
            new AppUser
            {
                UserName = "machine.admin@rentals.gr",
                Email = "machine.admin@rentals.gr",
                DisplayName = "Machine Admin",
                TenantId = tenantMachine,
                IsActive = true,
                EmailConfirmed = true
            },
            new AppUser
            {
                UserName = "warehouse.admin@rentals.gr",
                Email = "warehouse.admin@rentals.gr",
                DisplayName = "Warehouse Admin",
                TenantId = tenantWarehouse,
                IsActive = true,
                EmailConfirmed = true
            }
        };

        foreach (var user in users)
        {
            var exists = await context.Users
                .IgnoreQueryFilters()
                .AnyAsync(x => x.NormalizedEmail == user.Email.ToUpper());

            if (!exists)
            {
                var result = await userManager.CreateAsync(user, "Password123!");

                if (!result.Succeeded)
                {
                    throw new Exception(string.Join(", ",
                        result.Errors.Select(x => x.Description)));
                }

                if (user.TenantId == tenantBook)
                    bookUserId = user.Id;

                if (user.TenantId == tenantMachine)
                    machineUserId = user.Id;

                if (user.TenantId == tenantWarehouse)
                    warehouseUserId = user.Id;
            }
        }

        // ==========================================
        // ΔΗΜΙΟΥΡΓΙΑ Members
        // ==========================================
        var members = new[]
        {
            new Member
            {
                Id = bookUserId,
                FirstName = "Γιάννης",
                LastName = "Παπαδόπουλος",
                Afm = "123456789",
                Amka = "12345678901"
            },
            new Member
            {
                Id = machineUserId,
                FirstName = "Ελένη",
                LastName = "Κωνσταντίνου",
                Afm = "987654321",
                Amka = "10987654321"
            },
            new Member
            {
                Id = warehouseUserId,
                FirstName = "Κώστας",
                LastName = "Μακρίδης",
                Afm = "555555555",
                Amka = "55555555555"
            }
        };

        await context.Members.AddRangeAsync(members);
        await context.SaveChangesAsync();

        // ==========================================
        // 6. ΔΗΜΙΟΥΡΓΙΑ ASSET TYPES & FIELDS (Ορισμός Δομής)
        // ==========================================
        
        // --- ΤΥΠΟΣ 1: ΒΙΒΛΙΑ ---
        var bookType = new AssetType { Id = Guid.NewGuid(), TenantId = tenantBook, Name = "Βιβλία", Description = "Δανειστική Βιβλιοθήκη" };
        var bookFields = new List<AssetTypeField>
        {
            new() { Id = Guid.NewGuid(), AssetTypeId = bookType.Id, TenantId = tenantBook, Name = "isbn", Label = "ISBN", DataType = Enums.FieldDataType.Text, IsRequired = true, CreatedBy = bookUserId },
            new() { Id = Guid.NewGuid(), AssetTypeId = bookType.Id, TenantId = tenantBook, Name = "author", Label = "Συγγραφέας", DataType = Enums.FieldDataType.Text, IsRequired = true, CreatedBy = bookUserId },
            new() { Id = Guid.NewGuid(), AssetTypeId = bookType.Id, TenantId = tenantBook, Name = "pages", Label = "Σελίδες", DataType =  Enums.FieldDataType.Number, IsRequired = false, CreatedBy = bookUserId }
        };

        // --- ΤΥΠΟΣ 2: ΜΗΧΑΝΗΜΑΤΑ ---
        var machineType = new AssetType { Id = Guid.NewGuid(), TenantId = tenantMachine, Name = "Μηχανήματα", Description = "Βαρύς εξοπλισμός και εργαλεία" };
        var machineFields = new List<AssetTypeField>
        {
            new() { Id = Guid.NewGuid(), AssetTypeId = machineType.Id, TenantId = tenantMachine, Name = "watt", Label = "Ισχύς (Watt)", DataType = Enums.FieldDataType.Number, IsRequired = true, CreatedBy = machineUserId },
            new() { Id = Guid.NewGuid(), AssetTypeId = machineType.Id, TenantId = tenantMachine, Name = "brand", Label = "Μάρκα", DataType = Enums.FieldDataType.Text, IsRequired = true, CreatedBy = machineUserId },
            new() { Id = Guid.NewGuid(), AssetTypeId = machineType.Id, TenantId = tenantMachine, Name = "last_service", Label = "Τελευταίο Service", DataType = Enums.FieldDataType.Date, IsRequired = false, CreatedBy = machineUserId }
        };

        // --- ΤΥΠΟΣ 3: ΑΠΟΘΗΚΕΣ ---
        var warehouseType = new AssetType { Id = Guid.NewGuid(), TenantId = tenantWarehouse, Name = "Αποθήκες", Description = "Αποθηκευτικοί χώροι προς ενοικίαση" };
        var warehouseFields = new List<AssetTypeField>
        {
            new() { Id = Guid.NewGuid(), AssetTypeId = warehouseType.Id, TenantId = tenantWarehouse, Name = "address", Label = "Διεύθυνση", DataType = Enums.FieldDataType.Text, IsRequired = true, CreatedBy = warehouseUserId },
            new() { Id = Guid.NewGuid(), AssetTypeId = warehouseType.Id, TenantId = tenantWarehouse, Name = "region", Label = "Περιοχή", DataType = Enums.FieldDataType.Text, IsRequired = true, CreatedBy = warehouseUserId },
            new() { Id = Guid.NewGuid(), AssetTypeId = warehouseType.Id, TenantId = tenantWarehouse, Name = "size_sqm", Label = "Τετραγωνικά (τ.μ.)", DataType = Enums.FieldDataType.Number, IsRequired = true, CreatedBy = warehouseUserId },
            new() { Id = Guid.NewGuid(), AssetTypeId = warehouseType.Id, TenantId = tenantWarehouse, Name = "has_alarm", Label = "Συναγερμός", DataType = Enums.FieldDataType.Boolean, IsRequired = true, CreatedBy = warehouseUserId },
            new() { Id = Guid.NewGuid(), AssetTypeId = warehouseType.Id, TenantId = tenantWarehouse, Name = "door_code", Label = "Κωδικός Πόρτας", DataType = Enums.FieldDataType.Text, IsRequired = false, CreatedBy = warehouseUserId },
            new() { Id = Guid.NewGuid(), AssetTypeId = warehouseType.Id, TenantId = tenantWarehouse, Name = "type", Label = "Τύπος αποθήκης", DataType = Enums.FieldDataType.Text, IsRequired = false, CreatedBy = warehouseUserId }
        };

        await context.AssetTypes.AddRangeAsync(bookType, machineType, warehouseType);
        await context.AssetTypeFields.AddRangeAsync(bookFields);
        await context.AssetTypeFields.AddRangeAsync(machineFields);
        await context.AssetTypeFields.AddRangeAsync(warehouseFields);
        await context.SaveChangesAsync();


        // ==========================================
        // 7. ΔΗΜΙΟΥΡΓΙΑ CUSTOMERS (Πελάτες)
        // ==========================================
       var tenantIds = new[]
        {
            tenantBook,
            tenantMachine,
            tenantWarehouse
        };

        var allCustomers = new List<Customer>();

        // Θα φτιάξουμε 20 πελάτες για κάθε εταιρεία (σύνολο 300)
        foreach (var currentTenantId in tenantIds)
        {
            string currentUserId =
                currentTenantId == tenantBook ? bookUserId :
                currentTenantId == tenantMachine ? machineUserId :
                warehouseUserId;

            var customerFaker = new Faker<Customer>("el")
                .RuleFor(c => c.Id, f => Guid.NewGuid())
                .RuleFor(c => c.TenantId, _ => currentTenantId)
                .RuleFor(c => c.Type, f => f.Random.Enum<Enums.CustomerType>())
                .RuleFor(c => c.Name, (f, c) =>
                    c.Type == Enums.CustomerType.Person
                        ? f.Name.FullName()
                        : f.Company.CompanyName())
                .RuleFor(c => c.Afm,
                    f => (900000000 + f.IndexFaker).ToString())
                .RuleFor(c => c.Dou, f => f.PickRandom(greekDous))
                .RuleFor(c => c.Phones, f => "210" + f.Random.Replace("#######"))
                .RuleFor(c => c.Email, (f, c) => f.Internet.Email())
                .RuleFor(c => c.Address,
                    f => $"{f.Address.StreetName()} {f.Random.Number(1,150)}, {f.PickRandom(greekCities)}")
                .RuleFor(c => c.CreatedBy, _ => currentUserId)
                .RuleFor(c => c.CreatedAt, f => f.Date.Past().ToUniversalTime());

            allCustomers.AddRange(customerFaker.Generate(100));
        }

        await context.Customers.AddRangeAsync(allCustomers);
        await context.SaveChangesAsync();

        
        // ==========================================
        // 8. ΔΗΜΙΟΥΡΓΙΑ CONTACTS (Επαφές Πελατών)
        // ==========================================
        var faker = new Faker("el");
        var contacts = new List<Contact>();

        foreach(var customer in allCustomers)
        {
            for(int i=0;i<faker.Random.Int(1,3);i++)
            {
                contacts.Add(new Contact
                {
                    Id = Guid.NewGuid(),
                    CustomerId = customer.Id,
                    TenantId = customer.TenantId,
                    FirstName = faker.Name.FirstName(),
                    LastName = faker.Name.LastName(),
                    Email = faker.Internet.Email(),
                    Phone = faker.Phone.PhoneNumber(),
                    CanUseAsset = faker.Random.Bool(),
                    CreatedBy = customer.CreatedBy,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        await context.Contacts.AddRangeAsync(contacts);

        // ==========================================
        // 8. ΔΗΜΙΟΥΡΓΙΑ ASSETS & EAV VALUES (Παραγωγή)
        // ==========================================
        
        var assetsToInsert = new List<Asset>();
        var eavValuesToInsert = new List<AssetAttributeValue>();

        // Λογική δημιουργίας Assets (500 Βιβλία, 1500 Μηχανήματα, 50 Αποθήκες)
        var assetGenerationConfig = new List<(AssetType Type, List<AssetTypeField> Fields, int Count)>
        {
            (bookType, bookFields, 500),
            (machineType, machineFields, 1500),
            (warehouseType, warehouseFields, 50)
        };

        foreach (var config in assetGenerationConfig)
        {
            for (int i = 0; i < config.Count; i++)
            {
                var assetId = Guid.NewGuid();
                var propertiesDict = new Dictionary<string, object>();

                // --- 8.1 Παραγωγή EAV Τιμών ---
                foreach (var field in config.Fields)
                {
                    var eav = new AssetAttributeValue
                    {
                        Id = Guid.NewGuid(),
                        AssetId = assetId,
                        AssetTypeFieldId = field.Id,
                        TenantId = config.Type == bookType ? tenantBook : 
                                    config.Type == machineType ? tenantMachine :
                                    tenantWarehouse,
                        CreatedBy = config.Type ==bookType ? bookUserId :
                                    config.Type == machineType ? machineUserId :
                                    warehouseUserId,
                        CreatedAt = DateTime.UtcNow
                        // εχει και 4 πεδια για να αποθηκευτει η τιμη αναλογα με το data type, 
                        // τα οποια θα γεμιζουν αναλογα με το data type του πεδιου που ορισε ο χρηστης στο asset type field 
                        // (π.χ. StringValue αν ειναι text 
                        // DecimalValue αν ειναι number 
                        // DateValue αν ειναι date 
                        // BoolValue αν ειναι boolean)
                    };

                    // Γέμισμα δεδομένων βάσει του DataType και του Ονόματος του Πεδίου (για ρεαλισμό)
                    object jsonValue = null;

                    if (field.DataType == Enums.FieldDataType.Text)
                    {
                        if (field.Name == "isbn") eav.StringValue = faker.Commerce.Ean13();
                        else if (field.Name == "author") eav.StringValue = faker.PickRandom(authors);
                        else if (field.Name == "brand") eav.StringValue = faker.PickRandom(machineBrands);
                        else if (field.Name == "address") eav.StringValue = faker.Address.StreetAddress();
                        else if (field.Name == "region") eav.StringValue = faker.PickRandom(greekCities);
                        else if (field.Name == "door_code") eav.StringValue = faker.Random.Replace("####");
                        else if (field.Name == "type") eav.StringValue = faker.PickRandom(new[] { "Tax", "Storage" });
                        else eav.StringValue = faker.Lorem.Word();

                        jsonValue = eav.StringValue;
                    }
                    else if (field.DataType == Enums.FieldDataType.Number)
                    {
                        if (field.Name == "pages") eav.DecimalValue = faker.Random.Number(100, 800);
                        else if (field.Name == "watt") eav.DecimalValue = faker.Random.Number(500, 3000);
                        else if (field.Name == "size_sqm") eav.DecimalValue = faker.Random.Number(15, 500);
                        else eav.DecimalValue = faker.Random.Decimal(10, 100);

                        jsonValue = eav.DecimalValue;
                    }
                    else if (field.DataType == Enums.FieldDataType.Date)
                    {
                        eav.DateValue = faker.Date.Past().ToUniversalTime();
                        jsonValue = eav.DateValue;
                    }
                    else if (field.DataType == Enums.FieldDataType.Boolean)
                    {
                        eav.BoolValue = faker.Random.Bool();
                        jsonValue = eav.BoolValue;
                    }

                    eavValuesToInsert.Add(eav);
                    if (jsonValue != null)
                    {
                        propertiesDict.Add(field.Name, jsonValue); // Χρησιμοποιούμε το Name (π.χ. 'isbn') ως κλειδί στο JSON
                    }
                }

                // --- 8.2 Δημιουργία του βασικού Asset ---
                var assetName = config.Type.Name switch
                {
                    "Βιβλία" => faker.Commerce.ProductName() + " (Βιβλίο)",
                    "Μηχανήματα" => faker.Commerce.ProductAdjective() + " Εργαλείο",
                    "Αποθήκες" => $"Αποθήκη {faker.PickRandom(greekCities)}",
                    _ => faker.Commerce.ProductName()
                };

                var asset = new Asset
                {
                    Id = assetId,
                    AssetTypeId = config.Type.Id,
                    TenantId = config.Type == bookType ? tenantBook : 
                                    config.Type == machineType ? tenantMachine :
                                    tenantWarehouse,
                    Name = assetName,
                    Status = Enums.AssetStatus.Available,
                    AcquisitionCost = faker.Random.Decimal(50, 5000),
                    // Κάνουμε Serialize το Dictionary σε JSONB
                    PropertiesJson = JsonSerializer.Serialize(propertiesDict, new JsonSerializerOptions { WriteIndented = false }),
                    CreatedBy = config.Type ==bookType ? bookUserId :
                                    config.Type == machineType ? machineUserId :
                                    warehouseUserId,
                    CreatedAt = DateTime.UtcNow
                };

                assetsToInsert.Add(asset);
            }
        }

        await context.Assets.AddRangeAsync(assetsToInsert);
        await context.AssetAttributeValues.AddRangeAsync(eavValuesToInsert);

        await context.SaveChangesAsync();


        // ==========================================
        // 10. ΔΗΜΙΟΥΡΓΙΑ CONTRACTS & CONTRACT ASSETS
        // ==========================================
        var contractsToInsert = new List<Contract>();
        var contractAssetsToInsert = new List<ContractAsset>();
        var invoicesToInsert = new List<Invoice>();
        var paymentsToInsert = new List<Payment>();

        // Θα φτιάξουμε 15 τυχαία συμβόλαια
        for (int i = 0; i < 15; i++)
        {
            var contractId = Guid.NewGuid();
            var randomCustomer = faker.PickRandom(allCustomers);
            
            var netAmount = faker.Random.Decimal(100, 2000);
            var taxAmount = netAmount * 0.24m; // ΦΠΑ 24%
            var totalAmount = netAmount + taxAmount;
            
            var startDate = faker.Date.Past().ToUniversalTime();
            var endDate = startDate.AddMonths(faker.Random.Number(1, 12));

            var contract = new Contract
            {
                Id = contractId,
                CustomerId = randomCustomer.Id,
                TenantId = randomCustomer.TenantId,
                StartDate = startDate,
                EndDate = endDate,
                SignedDate = startDate.AddDays(-2),
                Terms = "Τυποποιημένοι όροι ενοικίασης παγίων...",
                AadeNumber = faker.Random.Replace("AADE-#########"),
                TotalAmount = Math.Round(totalAmount, 2),
                TaxAmount = Math.Round(taxAmount, 2),
                Status = endDate < DateTime.UtcNow ? Enums.RentalStatus.Completed : Enums.RentalStatus.Active,
                CreatedBy = randomCustomer.TenantId == tenantBook ? bookUserId :
                            randomCustomer.TenantId == tenantMachine ? machineUserId :
                            warehouseUserId,
                CreatedAt = DateTime.UtcNow
            };
            contractsToInsert.Add(contract);

            // -- Σύνδεση με 1 έως 3 τυχαία Assets --
            var numberOfAssets = faker.Random.Number(1, 3);
            // Παίρνουμε τυχαία Assets που είναι Διαθέσιμα
            var availableAssets = assetsToInsert.Where(a => a.Status == Enums.AssetStatus.Available).ToList();
            
            for (int j = 0; j < numberOfAssets; j++)
            {
                if (!availableAssets.Any()) break;

                var randomAsset = faker.PickRandom(availableAssets);
                availableAssets.Remove(randomAsset); // Για να μην το ξαναδιαλέξουμε στο ίδιο συμβόλαιο

                var contractAsset = new ContractAsset
                {
                    Id = Guid.NewGuid(),
                    ContractId = contractId,
                    AssetId = randomAsset.Id,
                    TenantId = randomCustomer.TenantId,
                    Notes = faker.Lorem.Sentence(),
                    CreatedBy = randomCustomer.TenantId == tenantBook ? bookUserId :
                                randomCustomer.TenantId == tenantMachine ? machineUserId :
                                warehouseUserId,
                    CreatedAt = DateTime.UtcNow
                };
                contractAssetsToInsert.Add(contractAsset);

                // Ενημερώνουμε το Status του Asset σε Rented (αφού πλέον νοικιάστηκε)
                randomAsset.Status = Enums.AssetStatus.Rented;
            }

            // ==========================================
            // 11. ΔΗΜΙΟΥΡΓΙΑ INVOICES & PAYMENTS
            // ==========================================
            // Φτιάχνουμε 1 τιμολόγιο για κάθε συμβόλαιο
            var invoiceId = Guid.NewGuid();
            var isPaid = faker.Random.Bool(); // 50% πιθανότητα να είναι εξοφλημένο
            
            var invoice = new Invoice
            {
                Id = invoiceId,
                ContractId = contractId,
                TenantId = randomCustomer.TenantId,
                InvoiceNumber = $"INV-{faker.Random.Number(1000, 9999)}",
                IssueDate = startDate,
                DueDate = startDate.AddDays(30), // Λήξη σε 30 μέρες
                TaxAmount = contract.TaxAmount,
                TotalAmount = contract.TotalAmount,
                IsPaid = isPaid,
                OutstandingBalance = isPaid ? 0 : contract.TotalAmount,
                CreatedBy = randomCustomer.TenantId == tenantBook ? bookUserId :
                            randomCustomer.TenantId == tenantMachine ? machineUserId :
                            warehouseUserId,
                CreatedAt = DateTime.UtcNow
            };
            invoicesToInsert.Add(invoice);

            // Αν κληρώθηκε ως Paid, του φτιάχνουμε και την αντίστοιχη πληρωμή
            if (isPaid)
            {
                var payment = new Payment
                {
                    Id = Guid.NewGuid(),
                    InvoiceId = invoiceId,
                    TenantId = randomCustomer.TenantId,
                    PaymentDate = startDate.AddDays(faker.Random.Number(1, 15)), // Πληρώθηκε εντός 15 ημερών
                    Amount = invoice.TotalAmount, // Εξόφληση όλου του ποσού
                    PaymentMethod = faker.PickRandom<Enums.PaymentMethod>(),
                    TransactionType = Enums.TransactionType.Income,
                    Notes = "Εξόφληση Τιμολογίου",
                    CreatedBy = randomCustomer.TenantId == tenantBook ? bookUserId :
                                randomCustomer.TenantId == tenantMachine ? machineUserId :
                                warehouseUserId,
                    CreatedAt = DateTime.UtcNow
                };
                paymentsToInsert.Add(payment);
            }
        }

        // Αποθηκεύουμε τα πάντα στη βάση με τη σωστή σειρά!
        await context.Contracts.AddRangeAsync(contractsToInsert);
        await context.ContractAssets.AddRangeAsync(contractAssetsToInsert);
        await context.Invoices.AddRangeAsync(invoicesToInsert);
        await context.Payments.AddRangeAsync(paymentsToInsert);
        
        
        await context.SaveChangesAsync();
        
        // Αποθήκευση όλων
        await context.SaveChangesAsync();
    }
}

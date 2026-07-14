using System.Text.Json;
using API.Data.Contexts;
using API.Entities;
using API.Interfaces;
using Bogus;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using static API.Entities.Enums;

namespace API.Data;

public class DbInitializer
{
    public static async Task InitializeAsync(
        AppDbContext context,
        UserManager<AppUser> userManager,
        ITenantProvider tenantProvider)
    {
        // 1. Δημιουργία της βάσης αν δεν υπάρχει
        await context.Database.EnsureCreatedAsync();

        // 2. Έλεγχος αν υπάρχουν ήδη δεδομένα
        if (await context.Tenants.IgnoreQueryFilters().AnyAsync())
            return;

        // ==========================================
        // 3. ΒΑΣΙΚΑ ΔΕΔΟΜΕΝΑ (Λίστες για Ρεαλισμό)
        // ==========================================
        var greekCities = new[]
        {
            "ΑΘΗΝΑ", "ΘΕΣΣΑΛΟΝΙΚΗ", "ΠΑΤΡΑ", "ΗΡΑΚΛΕΙΟ", "ΛΑΡΙΣΑ",
            "ΒΟΛΟΣ", "ΙΩΑΝΝΙΝΑ", "ΤΡΙΚΑΛΑ", "ΧΑΛΚΙΔΑ", "ΣΕΡΡΕΣ", "ΑΛΕΞΑΝΔΡΟΥΠΟΛΗ"
        };

        var greekDous = new[]
        {
            "Α' ΑΘΗΝΩΝ", "Δ' ΘΕΣΣΑΛΟΝΙΚΗΣ", "ΧΑΛΑΝΔΡΙΟΥ",
            "Α' ΠΕΡΙΣΤΕΡΙΟΥ", "ΓΛΥΦΑΔΑΣ", "ΗΡΑΚΛΕΙΟΥ"
        };

        var authors = new[]
        {
            "Νίκος Καζαντζάκης", "Οδυσσέας Ελύτης", "Γιώργος Σεφέρης",
            "Αλέξανδρος Παπαδιαμάντης", "Πηνελόπη Δέλτα"
        };

        var machineBrands = new[] { "Caterpillar", "Bosch", "Makita", "DeWalt", "Komatsu" };

        Randomizer.Seed = new Random(8675309);

        // ==========================================
        // 4. ΔΗΜΙΟΥΡΓΙΑ TENANTS
        // ==========================================
        string bookUserId    = "";
        string machineUserId = "";
        string warehouseUserId = "";

        var tenantBook      = Guid.NewGuid();
        var tenantMachine   = Guid.NewGuid();
        var tenantWarehouse = Guid.NewGuid();

        var tenants = new List<Tenant>
        {
            new() { Id = tenantBook,      Name = "Hellenic Rentals Book A.E.",  VatNumber = "099999999", ContactInfo = "Λεωφόρος Κηφισίας 150, Αθήνα",  SubscriptionStatus = SubscriptionStatus.Active },
            new() { Id = tenantMachine,   Name = "Rentals Machine A.E.",        VatNumber = "099929999", ContactInfo = "Λεωφόρος Αθηνων 150, Αθήνα",    SubscriptionStatus = SubscriptionStatus.Active },
            new() { Id = tenantWarehouse, Name = "Rentals Warehouse A.E.",      VatNumber = "099939999", ContactInfo = "Κηφισίας 150, Αθήνα",             SubscriptionStatus = SubscriptionStatus.Active }
        };

        await context.Tenants.AddRangeAsync(tenants);
        await context.SaveChangesAsync();

        // ==========================================
        // 5. ΔΗΜΙΟΥΡΓΙΑ USERS
        // ==========================================
        var users = new[]
        {
            new AppUser { UserName = "book.admin@rentals.gr",      Email = "book.admin@rentals.gr",      DisplayName = "Book Admin",      TenantId = tenantBook,      IsActive = true, EmailConfirmed = true },
            new AppUser { UserName = "machine.admin@rentals.gr",   Email = "machine.admin@rentals.gr",   DisplayName = "Machine Admin",   TenantId = tenantMachine,   IsActive = true, EmailConfirmed = true },
            new AppUser { UserName = "warehouse.admin@rentals.gr", Email = "warehouse.admin@rentals.gr", DisplayName = "Warehouse Admin", TenantId = tenantWarehouse, IsActive = true, EmailConfirmed = true }
        };

        foreach (var user in users)
        {
            var exists = await context.Users
                .IgnoreQueryFilters()
                .AnyAsync(x => x.NormalizedEmail == user.Email!.ToUpper());

            if (exists) continue;

            tenantProvider.SetCurrentTenant(user.TenantId);
            var result = await userManager.CreateAsync(user, "Password123!");

            if (!result.Succeeded)
                throw new Exception(string.Join(", ", result.Errors.Select(x => x.Description)));

            await userManager.AddToRoleAsync(user, "Admin");

            if (user.TenantId == tenantBook)      bookUserId      = user.Id;
            if (user.TenantId == tenantMachine)   machineUserId   = user.Id;
            if (user.TenantId == tenantWarehouse) warehouseUserId = user.Id;
        }

        // ==========================================
        // ΔΗΜΙΟΥΡΓΙΑ MEMBERS
        // ==========================================
        var members = new[]
        {
            new Member { Id = bookUserId,      FirstName = "Γιάννης", LastName = "Παπαδόπουλος", Afm = "123456789", Amka = "12345678901" },
            new Member { Id = machineUserId,   FirstName = "Ελένη",   LastName = "Κωνσταντίνου", Afm = "987654321", Amka = "10987654321" },
            new Member { Id = warehouseUserId, FirstName = "Κώστας",  LastName = "Μακρίδης",     Afm = "555555555", Amka = "55555555555" }
        };

        await context.Members.AddRangeAsync(members);
        await context.SaveChangesAsync();

        // ==========================================
        // 6. ΔΗΜΙΟΥΡΓΙΑ ASSET TYPES & FIELDS
        // ==========================================

        // --- ΤΥΠΟΣ 1: ΒΙΒΛΙΑ ---
        var bookType = new AssetType { Id = Guid.NewGuid(), TenantId = tenantBook, Name = "Βιβλία", Description = "Δανειστική Βιβλιοθήκη" };
        var bookFields = new List<AssetTypeField>
        {
            new() { Id = Guid.NewGuid(), AssetTypeId = bookType.Id, TenantId = tenantBook, Name = "isbn",   Label = "ISBN",         DataType = FieldDataType.Text,   IsRequired = true,  CreatedBy = bookUserId },
            new() { Id = Guid.NewGuid(), AssetTypeId = bookType.Id, TenantId = tenantBook, Name = "author", Label = "Συγγραφέας",   DataType = FieldDataType.Text,   IsRequired = true,  CreatedBy = bookUserId },
            new() { Id = Guid.NewGuid(), AssetTypeId = bookType.Id, TenantId = tenantBook, Name = "pages",  Label = "Σελίδες",      DataType = FieldDataType.Number, IsRequired = false, CreatedBy = bookUserId }
        };

        // --- ΤΥΠΟΣ 2: ΜΗΧΑΝΗΜΑΤΑ ---
        var machineType = new AssetType { Id = Guid.NewGuid(), TenantId = tenantMachine, Name = "Μηχανήματα", Description = "Βαρύς εξοπλισμός και εργαλεία" };
        var machineFields = new List<AssetTypeField>
        {
            new() { Id = Guid.NewGuid(), AssetTypeId = machineType.Id, TenantId = tenantMachine, Name = "watt",         Label = "Ισχύς (Watt)",        DataType = FieldDataType.Number, IsRequired = true,  CreatedBy = machineUserId },
            new() { Id = Guid.NewGuid(), AssetTypeId = machineType.Id, TenantId = tenantMachine, Name = "brand",        Label = "Μάρκα",               DataType = FieldDataType.Text,   IsRequired = true,  CreatedBy = machineUserId },
            new() { Id = Guid.NewGuid(), AssetTypeId = machineType.Id, TenantId = tenantMachine, Name = "last_service", Label = "Τελευταίο Service",   DataType = FieldDataType.Date,   IsRequired = false, CreatedBy = machineUserId }
        };

        // --- ΤΥΠΟΣ 3: ΑΠΟΘΗΚΕΣ ---
        var warehouseType = new AssetType { Id = Guid.NewGuid(), TenantId = tenantWarehouse, Name = "Αποθήκες", Description = "Αποθηκευτικοί χώροι προς ενοικίαση" };
        var warehouseFields = new List<AssetTypeField>
        {
            new() { Id = Guid.NewGuid(), AssetTypeId = warehouseType.Id, TenantId = tenantWarehouse, Name = "address",  Label = "Διεύθυνση",            DataType = FieldDataType.Text,    IsRequired = true,  CreatedBy = warehouseUserId },
            new() { Id = Guid.NewGuid(), AssetTypeId = warehouseType.Id, TenantId = tenantWarehouse, Name = "region",   Label = "Περιοχή",              DataType = FieldDataType.Text,    IsRequired = true,  CreatedBy = warehouseUserId },
            new() { Id = Guid.NewGuid(), AssetTypeId = warehouseType.Id, TenantId = tenantWarehouse, Name = "size_sqm", Label = "Τετραγωνικά (τ.μ.)",  DataType = FieldDataType.Number,  IsRequired = true,  CreatedBy = warehouseUserId },
            new() { Id = Guid.NewGuid(), AssetTypeId = warehouseType.Id, TenantId = tenantWarehouse, Name = "has_alarm",Label = "Συναγερμός",           DataType = FieldDataType.Boolean, IsRequired = true,  CreatedBy = warehouseUserId },
            new() { Id = Guid.NewGuid(), AssetTypeId = warehouseType.Id, TenantId = tenantWarehouse, Name = "door_code",Label = "Κωδικός Πόρτας",       DataType = FieldDataType.Text,    IsRequired = false, CreatedBy = warehouseUserId },
            new() { Id = Guid.NewGuid(), AssetTypeId = warehouseType.Id, TenantId = tenantWarehouse, Name = "type",     Label = "Τύπος αποθήκης",       DataType = FieldDataType.Text,    IsRequired = false, CreatedBy = warehouseUserId }
        };

        await context.AssetTypes.AddRangeAsync(bookType, machineType, warehouseType);
        await context.AssetTypeFields.AddRangeAsync(bookFields);
        await context.AssetTypeFields.AddRangeAsync(machineFields);
        await context.AssetTypeFields.AddRangeAsync(warehouseFields);
        await context.SaveChangesAsync();

        // Dropdown field για Μηχανήματα
        var colorField = new AssetTypeField
        {
            Id = Guid.NewGuid(), AssetTypeId = machineType.Id, TenantId = tenantMachine,
            Name = "color", Label = "Χρώμα", DataType = FieldDataType.Text,
            IsRequired = true, CreatedBy = machineUserId
        };

        var colorOptions = new List<AssetTypeFieldOption>
        {
            new() { Id = Guid.NewGuid(), AssetTypeFieldId = colorField.Id, TenantId = tenantMachine, Label = "Κόκκινο", Value = "red",   DisplayOrder = 1, CreatedBy = machineUserId },
            new() { Id = Guid.NewGuid(), AssetTypeFieldId = colorField.Id, TenantId = tenantMachine, Label = "Μαύρο",   Value = "black", DisplayOrder = 2, CreatedBy = machineUserId },
            new() { Id = Guid.NewGuid(), AssetTypeFieldId = colorField.Id, TenantId = tenantMachine, Label = "Λευκό",   Value = "white", DisplayOrder = 3, CreatedBy = machineUserId }
        };

        await context.AssetTypeFields.AddAsync(colorField);
        await context.AssetTypeFieldOptions.AddRangeAsync(colorOptions);
        await context.SaveChangesAsync();

        // ==========================================
        // 7. ΔΗΜΙΟΥΡΓΙΑ CUSTOMERS
        // ==========================================
        var tenantIds = new[] { tenantBook, tenantMachine, tenantWarehouse };
        var allCustomers = new List<Customer>();
        var faker = new Faker("el");

        foreach (var currentTenantId in tenantIds)
        {
            var currentUserId = currentTenantId == tenantBook ? bookUserId :
                                currentTenantId == tenantMachine ? machineUserId :
                                warehouseUserId;

            var customerFaker = new Faker<Customer>("el")
                .RuleFor(c => c.Id, _ => Guid.NewGuid())
                .RuleFor(c => c.TenantId, _ => currentTenantId)
                .RuleFor(c => c.Type, f => f.Random.Enum<CustomerType>())
                .RuleFor(c => c.Name, (f, c) =>
                    c.Type == CustomerType.Person ? f.Name.FullName() : f.Company.CompanyName())
                .RuleFor(c => c.Afm, f => (900000000 + f.IndexFaker).ToString())
                .RuleFor(c => c.Dou, f => f.PickRandom(greekDous))
                .RuleFor(c => c.Address, f => $"{f.Address.StreetName()} {f.Random.Number(1, 150)}, {f.PickRandom(greekCities)}")
                .RuleFor(c => c.CreatedBy, _ => currentUserId)
                .RuleFor(c => c.CreatedAt, f => f.Date.Past().ToUniversalTime());

            allCustomers.AddRange(customerFaker.Generate(100));
        }

        await context.Customers.AddRangeAsync(allCustomers);
        await context.SaveChangesAsync();

        // ==========================================
        // 8. ΔΗΜΙΟΥΡΓΙΑ CONTACTS
        // ==========================================
        var contacts = new List<Contact>();

        foreach (var customer in allCustomers)
        {
            for (int i = 0; i < faker.Random.Int(1, 3); i++)
            {
                contacts.Add(new Contact
                {
                    Id          = Guid.NewGuid(),
                    CustomerId  = customer.Id,
                    TenantId    = customer.TenantId,
                    Name   = faker.Name.FirstName() + " " + faker.Name.LastName(),
                    Email       = faker.Internet.Email(),
                    Phone       = faker.Phone.PhoneNumber(),
                    CanUseAsset = faker.Random.Bool(),
                    CreatedBy   = customer.CreatedBy,
                    CreatedAt   = DateTime.UtcNow
                });
            }
        }

        await context.Contacts.AddRangeAsync(contacts);

        // ==========================================
        // 9. ΔΗΜΙΟΥΡΓΙΑ ASSETS & EAV VALUES
        // ==========================================
        var assetsToInsert    = new List<Asset>();
        var eavValuesToInsert = new List<AssetAttributeValue>();

        var assetGenerationConfig = new List<(AssetType Type, List<AssetTypeField> Fields, int Count, Guid TenantId, string UserId)>
        {
            (bookType,      bookFields,      50,  tenantBook,      bookUserId),
            (machineType,   machineFields,   150, tenantMachine,   machineUserId),
            (warehouseType, warehouseFields, 50,  tenantWarehouse, warehouseUserId)
        };

        foreach (var cfg in assetGenerationConfig)
        {
            for (int i = 0; i < cfg.Count; i++)
            {
                var assetId       = Guid.NewGuid();
                var propertiesDict = new Dictionary<string, object>();

                foreach (var field in cfg.Fields)
                {
                    var eav = new AssetAttributeValue
                    {
                        Id               = Guid.NewGuid(),
                        AssetId          = assetId,
                        AssetTypeFieldId = field.Id,
                        TenantId         = cfg.TenantId,
                        CreatedBy        = cfg.UserId,
                        CreatedAt        = DateTime.UtcNow
                    };

                    object jsonValue = null!;

                    if (field.DataType == FieldDataType.Text)
                    {
                        eav.StringValue = field.Name switch
                        {
                            "isbn"      => faker.Commerce.Ean13(),
                            "author"    => faker.PickRandom(authors),
                            "brand"     => faker.PickRandom(machineBrands),
                            "address"   => faker.Address.StreetAddress(),
                            "region"    => faker.PickRandom(greekCities),
                            "door_code" => faker.Random.Replace("####"),
                            "type"      => faker.PickRandom(new[] { "Tax", "Storage" }),
                            _           => faker.Lorem.Word()
                        };
                        jsonValue = eav.StringValue;
                    }
                    else if (field.DataType == FieldDataType.Number)
                    {
                        eav.DecimalValue = field.Name switch
                        {
                            "pages"    => faker.Random.Number(100, 800),
                            "watt"     => faker.Random.Number(500, 3000),
                            "size_sqm" => faker.Random.Number(15, 500),
                            _          => faker.Random.Decimal(10, 100)
                        };
                        jsonValue = eav.DecimalValue;
                    }
                    else if (field.DataType == FieldDataType.Date)
                    {
                        eav.DateValue = faker.Date.Past().ToUniversalTime();
                        jsonValue     = eav.DateValue;
                    }
                    else if (field.DataType == FieldDataType.Boolean)
                    {
                        eav.BoolValue = faker.Random.Bool();
                        jsonValue     = eav.BoolValue;
                    }

                    eavValuesToInsert.Add(eav);
                    if (jsonValue != null)
                        propertiesDict[field.Name] = jsonValue;
                }

                var assetName = cfg.Type.Name switch
                {
                    "Βιβλία"      => faker.Commerce.ProductName() + " (Βιβλίο)",
                    "Μηχανήματα"  => faker.Commerce.ProductAdjective() + " Εργαλείο",
                    "Αποθήκες"    => $"Αποθήκη {faker.PickRandom(greekCities)}",
                    _             => faker.Commerce.ProductName()
                };

                assetsToInsert.Add(new Asset
                {
                    Id            = assetId,
                    AssetTypeId   = cfg.Type.Id,
                    TenantId      = cfg.TenantId,
                    Name          = assetName,
                    Status        = AssetStatus.Available,
                    Cost          = faker.Random.Decimal(10, 500),
                    RateUnit      = faker.PickRandom<RateUnit>(),
                    PropertiesJson = JsonDocument.Parse(JsonSerializer.Serialize(propertiesDict)),
                    CreatedBy     = cfg.UserId,
                    CreatedAt     = DateTime.UtcNow
                });
            }
        }

        await context.Assets.AddRangeAsync(assetsToInsert);
        await context.AssetAttributeValues.AddRangeAsync(eavValuesToInsert);
        await context.SaveChangesAsync();

        // ==========================================
        // 10. ΔΗΜΙΟΥΡΓΙΑ CONTRACTS & CONTRACT ASSETS
        // ==========================================
        var contractsToInsert      = new List<Contract>();
        var contractAssetsToInsert = new List<ContractAsset>();
        var paymentsToInsert       = new List<Payment>();
        var paymentContractsToInsert = new List<PaymentContract>();

        // Διαθέσιμα assets ανά tenant για να αποφύγουμε διπλοεπιλογή
        var availablePerTenant = new Dictionary<Guid, List<Asset>>
        {
            [tenantBook]      = assetsToInsert.Where(a => a.TenantId == tenantBook).ToList(),
            [tenantMachine]   = assetsToInsert.Where(a => a.TenantId == tenantMachine).ToList(),
            [tenantWarehouse] = assetsToInsert.Where(a => a.TenantId == tenantWarehouse).ToList()
        };

        for (int i = 0; i < 15; i++)
        {
            var contractId      = Guid.NewGuid();
            var randomCustomer  = faker.PickRandom(allCustomers);
            var tenantId        = randomCustomer.TenantId;
            var currentUserId   = tenantId == tenantBook ? bookUserId :
                                  tenantId == tenantMachine ? machineUserId :
                                  warehouseUserId;

            var startDate = faker.Date.Past(1).ToUniversalTime();
            var endDate   = startDate.AddMonths(faker.Random.Number(1, 12));
            var days      = Math.Max(1, (int)(endDate - startDate).TotalDays);

            // ── ContractAssets + υπολογισμός ποσών ──────────────────────
            var pool           = availablePerTenant[tenantId];
            var numberOfAssets = Math.Min(faker.Random.Number(1, 3), pool.Count);
            decimal netTotal   = 0;

            for (int j = 0; j < numberOfAssets; j++)
            {
                if (pool.Count == 0) break;

                var randomAsset = faker.PickRandom(pool);
                pool.Remove(randomAsset);
                randomAsset.Status = AssetStatus.Rented;

                var unitCost   = randomAsset.Cost > 0 ? randomAsset.Cost : faker.Random.Decimal(50, 300);
                var calculated = CalculateRentalAmount(randomAsset.RateUnit, unitCost, days);
                netTotal      += calculated;

                contractAssetsToInsert.Add(new ContractAsset
                {
                    Id               = Guid.NewGuid(),
                    ContractId       = contractId,
                    AssetId          = randomAsset.Id,
                    StartDate        = startDate,
                    EndDate          = endDate,
                    UnitCost         = Math.Round(unitCost, 2),
                    RateUnit         = randomAsset.RateUnit,
                    CalculatedAmount = calculated,
                    Notes            = faker.Lorem.Sentence()
                });
            }

            // Αν δεν έχουμε assets, χρησιμοποιούμε τυχαίο ποσό
            if (netTotal == 0)
                netTotal = faker.Random.Decimal(200, 2000);

            var taxAmount   = Math.Round(netTotal * 0.24m, 2);
            var totalAmount = Math.Round(netTotal + taxAmount, 2);
            var refCode     = $"REF-{tenantId.ToString()[..4].ToUpper()}-{i + 1:D4}";

            var isCompleted = endDate < DateTime.UtcNow;

            var contract = new Contract
            {
                Id                   = contractId,
                CustomerId           = randomCustomer.Id,
                TenantId             = tenantId,
                StartDate            = startDate,
                EndDate              = endDate,
                SignedDate           = startDate.AddDays(-2),
                Terms                = "Τυποποιημένοι όροι ενοικίασης παγίων...",
                TotalAmount          = totalAmount,
                TaxAmount            = taxAmount,
                DiscountAmount       = 0,
                ReferenceCode        = refCode,
                InstallmentFrequency = InstallmentFrequency.Monthly,
                Status               = isCompleted ? RentalStatus.Completed : RentalStatus.Active,
                CreatedBy            = currentUserId,
                CreatedAt            = DateTime.UtcNow
            };
            contractsToInsert.Add(contract);

            // ── Πληρωμή για ολοκληρωμένα συμβόλαια (50% πιθανότητα) ──────
            if (isCompleted && faker.Random.Bool())
            {
                var paymentId = Guid.NewGuid();

                paymentsToInsert.Add(new Payment
                {
                    Id                  = paymentId,
                    TenantId            = tenantId,
                    PaymentDate         = startDate.AddDays(faker.Random.Number(1, 15)),
                    Amount              = totalAmount,
                    UnallocatedAmount   = totalAmount,
                    PaymentMethod       = faker.PickRandom<PaymentMethod>(),
                    TransactionType     = TransactionType.Income,
                    MatchStatus         = PaymentMatchStatus.Unmatched,
                    TenantReferenceCode = refCode,
                    Notes               = "Εξόφληση Συμβολαίου",
                    CreatedBy           = currentUserId,
                    CreatedAt           = DateTime.UtcNow
                });

                paymentContractsToInsert.Add(new PaymentContract
                {
                    PaymentId  = paymentId,
                    ContractId = contractId
                });
            }
        }

        // Αποθήκευση με τη σωστή σειρά (FKs)
        await context.Contracts.AddRangeAsync(contractsToInsert);
        await context.ContractAssets.AddRangeAsync(contractAssetsToInsert);
        await context.SaveChangesAsync();

        await context.Payments.AddRangeAsync(paymentsToInsert);
        await context.SaveChangesAsync();

        if (paymentContractsToInsert.Count > 0)
        {
            await context.PaymentContracts.AddRangeAsync(paymentContractsToInsert);
            await context.SaveChangesAsync();
        }
    }

    private static decimal CalculateRentalAmount(RateUnit rateUnit, decimal unitCost, int days)
    {
        return rateUnit switch
        {
            RateUnit.PerHour  => Math.Round(unitCost * days * 8, 2),
            RateUnit.PerDay   => Math.Round(unitCost * days, 2),
            RateUnit.PerMonth => Math.Round(unitCost * (decimal)Math.Ceiling(days / 30.0), 2),
            RateUnit.Sale     => unitCost,
            _                 => unitCost
        };
    }
}
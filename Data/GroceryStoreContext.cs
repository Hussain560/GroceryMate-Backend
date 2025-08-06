using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using GroceryMateApi.Models;

namespace GroceryMateApi.Data
{
    public class GroceryStoreContext : IdentityDbContext<ApplicationUser, ApplicationRole, int>
    {
        public GroceryStoreContext(DbContextOptions<GroceryStoreContext> options)
            : base(options)
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);

            // Suppress the pending model changes warning
            optionsBuilder.ConfigureWarnings(warnings =>
                warnings.Ignore(RelationalEventId.ModelValidationKeyDefaultValueWarning)
                       .Ignore(RelationalEventId.PendingModelChangesWarning));
        }

        public DbSet<Product> Products { get; set; } = null!;
        public DbSet<Category> Categories { get; set; } = null!;
        public DbSet<Supplier> Suppliers { get; set; } = null!;
        public DbSet<Brand> Brands { get; set; } = null!;
        public DbSet<InventoryTransaction> InventoryTransactions { get; set; } = null!;
        public DbSet<Sale> Sales { get; set; } = null!;
        public DbSet<SaleDetail> SaleDetails { get; set; } = null!;
        public DbSet<Invoice> Invoices { get; set; } = null!;
        public DbSet<Expense> Expenses { get; set; } = null!; // Add this line
        public DbSet<ProductBatch> ProductBatches { get; set; } = null!; // Add missing DbSet

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Identity table configurations
            modelBuilder.Entity<ApplicationUser>().ToTable("AspNetUsers");
            modelBuilder.Entity<ApplicationRole>().ToTable("AspNetRoles");
            modelBuilder.Entity<IdentityUserRole<int>>().ToTable("AspNetUserRoles");
            modelBuilder.Entity<IdentityUserClaim<int>>().ToTable("AspNetUserClaims");
            modelBuilder.Entity<IdentityUserLogin<int>>().ToTable("AspNetUserLogins");
            modelBuilder.Entity<IdentityRoleClaim<int>>().ToTable("AspNetRoleClaims");
            modelBuilder.Entity<IdentityUserToken<int>>().ToTable("AspNetUserTokens");

            // Constraints and relationships
            // Unique constraints
            modelBuilder.Entity<Product>()
                .HasIndex(p => p.Barcode)
                .IsUnique()
                .HasFilter("[Barcode] IS NOT NULL");

            modelBuilder.Entity<Category>()
                .HasIndex(c => c.CategoryName)
                .IsUnique();

            // Fixed check constraints
            modelBuilder.Entity<Product>(entity =>
            {
                entity.ToTable("Products");
                entity.Property(p => p.DiscountPercentage)
                    .HasColumnType("decimal(5,2)")
                    .HasDefaultValue(0.00m);

                // Computed column for TotalStockQuantity (optional)
                entity.Property(p => p.TotalStockQuantity)
                    .HasComputedColumnSql("(SELECT SUM([StockQuantity]) FROM [ProductBatches] WHERE [ProductID] = [ProductID])", stored: false);
            });

            // Additional relationships
            modelBuilder.Entity<Product>()
                .HasOne(p => p.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Product>()
                .HasOne(p => p.Supplier)
                .WithMany(s => s.Products)
                .HasForeignKey(p => p.SupplierID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ApplicationUser>()
                .HasOne(u => u.Role)
                .WithMany(r => r.Users)
                .HasForeignKey(u => u.RoleID)
                .OnDelete(DeleteBehavior.Restrict);

            // Decimal precision
            modelBuilder.Entity<Product>()
                .Property(p => p.UnitPrice)
                .HasPrecision(18, 2);

            modelBuilder.Entity<SaleDetail>()
                .Property(sd => sd.UnitPrice)
                .HasPrecision(18, 2);

            // Update SaleDetail configurations
            modelBuilder.Entity<SaleDetail>(entity =>
            {
                // Remove old Subtotal configuration
                // entity.Property(sd => sd.Subtotal).HasPrecision(18, 2);  // Remove this line

                // Add new property configurations
                entity.Property(sd => sd.LineSubtotalBeforeDiscount).HasPrecision(18, 2);
                entity.Property(sd => sd.LineDiscountAmount).HasPrecision(18, 2);
                entity.Property(sd => sd.LineSubtotalAfterDiscount).HasPrecision(18, 2);
                entity.Property(sd => sd.LineVATAmount).HasPrecision(18, 2);
                entity.Property(sd => sd.VATPercentage).HasPrecision(5, 2).HasDefaultValue(15.00M);
                entity.Property(sd => sd.LineFinalTotal).HasPrecision(18, 2);
                entity.Property(sd => sd.OriginalUnitPrice).HasPrecision(18, 2);
                entity.Property(sd => sd.UnitPriceAfterDiscount).HasPrecision(18, 2);
                entity.Property(sd => sd.DiscountPercentage).HasPrecision(5, 2).HasDefaultValue(0.00m);
            });

            // Cascade delete behavior
            modelBuilder.Entity<Sale>()
                .HasMany(s => s.SaleDetails)
                .WithOne(sd => sd.Sale)
                .OnDelete(DeleteBehavior.Cascade);

            // Add Sale entity configuration
            modelBuilder.Entity<Sale>(entity =>
            {
                // Primary key and relationships already configured...

                // Configure decimal precision for money values
                entity.Property(s => s.CashReceived).HasPrecision(18, 2);
                entity.Property(s => s.Change).HasPrecision(18, 2);
                entity.Property(s => s.SubtotalBeforeDiscount).HasPrecision(18, 2);
                entity.Property(s => s.TotalDiscountAmount).HasPrecision(18, 2);
                entity.Property(s => s.SubtotalAfterDiscount).HasPrecision(18, 2);
                entity.Property(s => s.TotalVATAmount).HasPrecision(18, 2);
                entity.Property(s => s.FinalTotal).HasPrecision(18, 2);

                // Configure percentage values with lower precision
                entity.Property(s => s.TotalDiscountPercentage).HasPrecision(5, 2);
                entity.Property(s => s.VATPercentage).HasPrecision(5, 2).HasDefaultValue(15.00M);

                // Invoice number should be required and have max length
                entity.Property(s => s.InvoiceNumber)
                    .IsRequired()
                    .HasMaxLength(20);

                // Payment method should be required and have max length
                entity.Property(s => s.PaymentMethod)
                    .IsRequired()
                    .HasMaxLength(10)
                    .HasDefaultValue("Cash");

                // Optional customer fields max length
                entity.Property(s => s.CustomerName).HasMaxLength(100);
                entity.Property(s => s.CustomerPhone).HasMaxLength(20);

                // Add indexes for common queries
                entity.HasIndex(s => s.SaleDate);
                entity.HasIndex(s => s.InvoiceNumber).IsUnique();
                entity.HasIndex(s => s.CreatedAt);

                // Add ReturnAmount column
                entity.Property(s => s.ReturnAmount).HasPrecision(18, 2).IsRequired(false);
            });

            // Seed data
            // 1. Seed Roles
            modelBuilder.Entity<ApplicationRole>().HasData(
                new ApplicationRole { Id = 1, Name = "Manager", NormalizedName = "MANAGER" },
                new ApplicationRole { Id = 2, Name = "Employee", NormalizedName = "EMPLOYEE" }
            );

            // 2. Seed Users
            var hasher = new PasswordHasher<ApplicationUser>();
            var adminUser = new ApplicationUser
            {
                Id = 1,
                UserName = "admin@store.com",
                NormalizedUserName = "ADMIN@STORE.COM",
                Email = "admin@store.com",
                NormalizedEmail = "ADMIN@STORE.COM",
                EmailConfirmed = true,
                FullName = "System Administrator",
                CreatedAt = DateTime.UtcNow,
                SecurityStamp = Guid.NewGuid().ToString()
            };
            adminUser.PasswordHash = hasher.HashPassword(adminUser, "Admin123!");

            var employeeUser = new ApplicationUser
            {
                Id = 2,
                UserName = "employee@store.com",
                NormalizedUserName = "EMPLOYEE@STORE.COM",
                Email = "employee@store.com",
                NormalizedEmail = "EMPLOYEE@STORE.COM",
                EmailConfirmed = true,
                FullName = "Store Employee",
                CreatedAt = DateTime.UtcNow,
                SecurityStamp = Guid.NewGuid().ToString()
            };
            employeeUser.PasswordHash = hasher.HashPassword(employeeUser, "Employee123!");

            modelBuilder.Entity<ApplicationUser>().HasData(adminUser, employeeUser);

            // 3. Seed User Roles
            modelBuilder.Entity<IdentityUserRole<int>>().HasData(
                new IdentityUserRole<int> { UserId = 1, RoleId = 1 },
                new IdentityUserRole<int> { UserId = 2, RoleId = 2 }
            );

            // 4. Seed Categories, Brands, Suppliers, and Products
            modelBuilder.Entity<Category>().HasData(
                new Category { CategoryID = 1, CategoryName = "Bakery", ImagePath = "/images/categories/bakery.jpeg" },
                new Category { CategoryID = 2, CategoryName = "Beverages", ImagePath = "/images/categories/beverages.jpg" },
                new Category { CategoryID = 3, CategoryName = "Breakfast", ImagePath = "/images/categories/breakfast.jpg" },
                new Category { CategoryID = 4, CategoryName = "Cheese & Deli", ImagePath = "/images/categories/cheese-deli.jpg" },
                new Category { CategoryID = 5, CategoryName = "Cooking Essentials", ImagePath = "/images/categories/cooking-essentials.jpg" },
                new Category { CategoryID = 6, CategoryName = "Dairy", ImagePath = "/images/categories/dairy.jpg" },
                new Category { CategoryID = 7, CategoryName = "Food Basics", ImagePath = "/images/categories/food-basics.jpg" },
                new Category { CategoryID = 8, CategoryName = "Frozen Products", ImagePath = "/images/categories/frozen.jpg" },
                //new Category { CategoryID = 9, CategoryName = "Fruits & Vegetables", ImagePath = "/images/categories/fruits-vegetables.jpg" },
                new Category { CategoryID = 10, CategoryName = "Home Care & Laundry", ImagePath = "/images/categories/home-care.jpg" },
                new Category { CategoryID = 11, CategoryName = "Personal Care", ImagePath = "/images/categories/personal-care.jpg" },
                new Category { CategoryID = 12, CategoryName = "Snacks", ImagePath = "/images/categories/snacks.jpg" },
                new Category { CategoryID = 13, CategoryName = "Water", ImagePath = "/images/categories/water.jpg" },
                new Category { CategoryID = 14, CategoryName = "Ready to Eat", ImagePath = "/images/categories/ready-to-eat.png" }
            );

            modelBuilder.Entity<Brand>().HasData(
                // Dairy & Beverages
                new Brand { BrandID = 1, BrandName = "Almarai", ImageUrl = "/images/brands/almarai.jpg" },
                new Brand { BrandID = 2, BrandName = "Al Safi Danone", ImageUrl = "/images/brands/alsafi.jpg" },
                new Brand { BrandID = 3, BrandName = "Nadec", ImageUrl = "/images/brands/nadec.jpg" },
                new Brand { BrandID = 4, BrandName = "Nova", ImageUrl = "/images/brands/nova.jpg" },

                // Food & Snacks
                new Brand { BrandID = 5, BrandName = "Al Kabeer", ImageUrl = "/images/brands/alkabeer.jpg" },
                new Brand { BrandID = 6, BrandName = "Americana", ImageUrl = "/images/brands/americana.jpg" },
                new Brand { BrandID = 7, BrandName = "Goody", ImageUrl = "/images/brands/goody.jpg" },
                new Brand { BrandID = 8, BrandName = "Luna", ImageUrl = "/images/brands/luna.jpg" },

                // Beverages
                new Brand { BrandID = 9, BrandName = "Al Rabie", ImageUrl = "/images/brands/alrabie.jpg" },
                new Brand { BrandID = 10, BrandName = "Vimto", ImageUrl = "/images/brands/vimto.jpg" },

                // Personal Care
                new Brand { BrandID = 11, BrandName = "Al Jamal", ImageUrl = "/images/brands/aljamal.jpg" },
                new Brand { BrandID = 12, BrandName = "Fine", ImageUrl = "/images/brands/fine.jpg" },

                // Water
                new Brand { BrandID = 13, BrandName = "Aquafina", ImageUrl = "/images/brands/aquafina.jpg" },
                new Brand { BrandID = 14, BrandName = "Nova Water", ImageUrl = "/images/brands/nova-water.jpg" },
                new Brand { BrandID = 15, BrandName = "Al Manhal", ImageUrl = "/images/brands/almanhal.jpg" },

                // Cooking Essentials
                new Brand { BrandID = 16, BrandName = "Al Osra", ImageUrl = "/images/brands/alosra.jpg" },
                new Brand { BrandID = 17, BrandName = "Nada", ImageUrl = "/images/brands/nada.jpg" },
                new Brand { BrandID = 18, BrandName = "Sadia", ImageUrl = "/images/brands/sadia.jpg" },

                // International Brands in KSA
                new Brand { BrandID = 19, BrandName = "Nestlé", ImageUrl = "/images/brands/nestle.jpg" },
                new Brand { BrandID = 20, BrandName = "Kraft", ImageUrl = "/images/brands/kraft.jpg" },
                new Brand { BrandID = 21, BrandName = "Kellogg's", ImageUrl = "/images/brands/kelloggs.jpg" },
                new Brand { BrandID = 22, BrandName = "Lipton", ImageUrl = "/images/brands/lipton.jpg" },

                // More Local Brands
                new Brand { BrandID = 23, BrandName = "Herfy", ImageUrl = "/images/brands/herfy.jpg" },
                new Brand { BrandID = 24, BrandName = "Al Shifa", ImageUrl = "/images/brands/alshifa.jpg" },
                new Brand { BrandID = 25, BrandName = "Al Watania", ImageUrl = "/images/brands/alwatania.jpg" },
                new Brand { BrandID = 26, BrandName = "Reem", ImageUrl = "/images/brands/reem.jpg" },
                new Brand { BrandID = 27, BrandName = "Al Faris", ImageUrl = "/images/brands/alfaris.jpg" },
                new Brand { BrandID = 28, BrandName = "Al Tazaj", ImageUrl = "/images/brands/altazaj.jpg" },
                new Brand { BrandID = 29, BrandName = "Kudu", ImageUrl = "/images/brands/kudu.jpg" },
                new Brand { BrandID = 30, BrandName = "Afia", ImageUrl = "/images/brands/afia.jpg" }
            );

            // 6. Suppliers
            modelBuilder.Entity<Supplier>().HasData(
                new Supplier { SupplierID = 1, SupplierName = "Fresh Foods Co", ContactName = "John Smith", Phone = "1234567890" },
                new Supplier { SupplierID = 2, SupplierName = "Dairy Best", ContactName = "Mary Johnson", Phone = "0987654321" }
            );

            // 7. Products
            modelBuilder.Entity<Product>().HasData(
               new Product
               {
                   ProductID = 1,
                   ProductName = "Fresh Arabic Bread",
                   CategoryID = 1,
                   SupplierID = 1,
                   BrandID = 23,
                   UnitPrice = 1.99M,
                   DiscountPercentage = 10.00m,
                   ReorderLevel = 0,
                   Barcode = "6291001001012",
                   ImageUrl = "https://img.ananinja.com/media/ninja-catalog-42/35173bab-8fd1-43b4-9b24-1968a4f83f65_ArabicBreadWhite.png?w=1920&q=75",
                   CreatedAt = DateTime.Parse("2025-06-16T01:58:17.2228023")
               },
               new Product
               {
                   ProductID = 2,
                   ProductName = "Bread Sweet Brioche Chocolate 140 G",
                   CategoryID = 1,
                   BrandID = 29, // Kudu
                   SupplierID = 1,
                   UnitPrice = 5.99M,
                   DiscountPercentage = 15.00m,
                   ReorderLevel = 0,
                   Barcode = "6291001001029",
                   ImageUrl = "https://store.nana.sa/_next/image?url=https%3A%2F%2Fcdn.nana.sa%2Fcatalog%2Flarge%2F0%2F6%2F3%2F4%2F06349cc79845d27fcdee53877157434e45d69d25_6287002130049.jpg&w=1200&q=75",
                   CreatedAt = DateTime.Parse("2025-06-16T01:58:17.2228219")
               },
               new Product
               {
                   ProductID = 3,
                   ProductName = "Orange Juice 1L",
                   CategoryID = 2,
                   BrandID = 9, // Al Rabie
                   SupplierID = 2,
                   UnitPrice = 7.50M,
                   DiscountPercentage = 0.00m, // No discount
                   ReorderLevel = 0,
                   Barcode = "6291001001036",
                   ImageUrl = "https://cdn.mafrservices.com/pim-content/SAU/media/product/116989/1721309405/116989_main.jpg",
                   CreatedAt = DateTime.Parse("2025-06-16T01:58:17.2228225")
               },
               new Product
               {
                   ProductID = 4,
                   ProductName = "Al Rabie Berry Mix Juice, 1 Litre - Pack of 1- Pack May Vary",
                   CategoryID = 2,
                   BrandID = 9, // Al Rabie
                   SupplierID = 2,
                   UnitPrice = 2.99M,
                   DiscountPercentage = 5.00m, // 5% discount
                   ReorderLevel = 0,
                   Barcode = "6291001001043",
                   ImageUrl = "https://m.media-amazon.com/images/I/81+l9hI4nPL._AC_SY879_.jpg",
                   CreatedAt = DateTime.Parse("2025-06-16T01:58:17.2228229")
               },
               new Product
               {
                   ProductID = 5,
                   ProductName = "Fresh Milk 2L",
                   CategoryID = 6,
                   BrandID = 1, // Almarai
                   SupplierID = 1,
                   UnitPrice = 11.00M,
                   DiscountPercentage = 0.00m, // No discount
                   ReorderLevel = 0,
                   Barcode = "6291001001050",
                   ImageUrl = "https://cdn.mafrservices.com/sys-master-root/hba/h9b/50520415993886/106475_main.jpg?im=Resize=480",
                   CreatedAt = DateTime.Parse("2025-06-16T01:58:17.2228268")
               },
               new Product
               {
                   ProductID = 6,
                   ProductName = "0% Fat Greek Yoghurt 160g",
                   CategoryID = 6,
                   BrandID = 17, // From the second dataset, Al Safi Danone from first dataset
                   SupplierID = 1,
                   UnitPrice = 4.50M,
                   DiscountPercentage = 0.00m, // No discount
                   ReorderLevel = 0,
                   Barcode = "6291001001067",
                   ImageUrl = "https://cdn.mafrservices.com/sys-master-root/hdd/h2c/63422372839454/597424_main.jpg?im=Resize=480",
                   CreatedAt = DateTime.Parse("2025-06-16T01:58:17.2228272")
               },
               new Product
               {
                   ProductID = 7,
                   ProductName = "Classic Potato Chips, 28.3g - Pack of 1",
                   CategoryID = 12,
                   BrandID = 8, // Luna
                   SupplierID = 2,
                   UnitPrice = 2.00M,
                   DiscountPercentage = 25.00m, // 25% discount
                   ReorderLevel = 0,
                   Barcode = "6291001001074",
                   ImageUrl = "https://m.media-amazon.com/images/I/71VwH-VGe6L._AC_SX569_.jpg",
                   CreatedAt = DateTime.Parse("2025-06-16T01:58:17.2228275")
               },
               new Product
               {
                   ProductID = 8,
                   ProductName = "Mixed Nuts Deluxe 120 g",
                   CategoryID = 12,
                   BrandID = 7, // Goody
                   SupplierID = 2,
                   UnitPrice = 15.99M,
                   DiscountPercentage = 0.00m, // No discount
                   ReorderLevel = 0,
                   Barcode = "6291001001081",
                   ImageUrl = "https://m.media-amazon.com/images/I/41hNkB0UiyL._AC_.jpg",
                   CreatedAt = DateTime.Parse("2025-06-16T01:58:17.2228279")
               },
               new Product
               {
                   ProductID = 9,
                   ProductName = "Water 6x1.5L",
                   CategoryID = 13,
                   BrandID = 13, // Aquafina
                   SupplierID = 1,
                   UnitPrice = 8.25M,
                   DiscountPercentage = 5.00m, // 5% discount
                   ReorderLevel = 0,
                   Barcode = "6291001001098",
                   ImageUrl = "https://images.todoorstep.com/product/2305180/En.jpg?t=1747834652",
                   CreatedAt = DateTime.Parse("2025-06-16T01:58:17.2228283")
               },
               new Product
               {
                   ProductID = 10,
                   ProductName = "Bottled Water 330ml",
                   CategoryID = 13,
                   BrandID = 14, // Nova Water
                   SupplierID = 1,
                   UnitPrice = 2.99M,
                   DiscountPercentage = 0.00m, // No discount
                   ReorderLevel = 0,
                   Barcode = "6291001001104",
                   ImageUrl = "https://cdn.mafrservices.com/sys-master-root/h75/ha4/9400079974430/51395_main.jpg?im=Resize=480",
                   CreatedAt = DateTime.Parse("2025-06-16T01:58:17.2228287")
               },
               new Product
               {
                   ProductID = 11,
                   ProductName = "Labneh 500g",
                   CategoryID = 6,
                   BrandID = 1, // Almarai
                   SupplierID = 1,
                   UnitPrice = 8.50M,
                   DiscountPercentage = 15.00m, // 15% discount
                   ReorderLevel = 0,
                   Barcode = "6291001001111",
                   ImageUrl = "https://almmedia.almarai.com/Gallery/35530-LABNEH-FF-400G-(1X28)-EN513202560117AM_sys513202560333AM.webp",
                   CreatedAt = DateTime.Parse("2025-06-16T01:58:17.2228290")
               },
               new Product
               {
                   ProductID = 12,
                   ProductName = "Feta White Cheese 500g",
                   CategoryID = 4, // Changed from 6 to 4 based on the INSERT statement
                   BrandID = 2, // Al Safi Danone
                   SupplierID = 1,
                   UnitPrice = 12.75M, // Changed from 12.99 to 12.75
                   DiscountPercentage = 0.00m, // No discount
                   ReorderLevel = 0,
                   Barcode = "6291001001128",
                   ImageUrl = "https://cdn.mafrservices.com/sys-master-root/h62/h02/33685666725918/684779_main.jpg?im=Resize=480",
                   CreatedAt = DateTime.Parse("2025-06-16T01:58:17.2228294")
               },
               new Product
               {
                   ProductID = 13,
                   ProductName = "Corn Flakes 500g",
                   CategoryID = 3,
                   BrandID = 21, // Kellogg's
                   SupplierID = 2,
                   UnitPrice = 28.00M, // Changed from 15.99 to 28.00
                   DiscountPercentage = 0.00m, // No discount
                   ReorderLevel = 0,
                   Barcode = "6291001001135",
                   ImageUrl = "https://cdn.mafrservices.com/sys-master-root/h46/h81/12844245680158/95739_main.jpg?im=Resize=480",
                   CreatedAt = DateTime.Parse("2025-06-16T01:58:17.2228297")
               },
               new Product
               {
                   ProductID = 14,
                   ProductName = "Honey 500g",
                   CategoryID = 3,
                   BrandID = 24, // Al Shifa
                   SupplierID = 2,
                   UnitPrice = 29.99M,
                   DiscountPercentage = 0.00m, // No discount
                   ReorderLevel = 0,
                   Barcode = "6291001001142",
                   ImageUrl = "https://m.media-amazon.com/images/I/51hPMDNdqZL._AC_SX679_.jpg",
                   CreatedAt = DateTime.Parse("2025-06-16T01:58:17.2228300")
               },
               new Product
               {
                   ProductID = 15,
                   ProductName = "Sunflower Oil 1.5L", // Changed from 1.8L to 1.5L
                   CategoryID = 5,
                   BrandID = 30, // Afia
                   SupplierID = 1,
                   UnitPrice = 24.75M, // Changed from 24.99 to 24.75
                   DiscountPercentage = 5.00m, // 5% discount
                   ReorderLevel = 0,
                   Barcode = "6291001001159",
                   ImageUrl = "https://cdn.mafrservices.com/sys-master-root/h38/hd2/61652639809566/637206_main.jpg?im=Resize=480",
                   CreatedAt = DateTime.Parse("2025-06-16T01:58:17.2228304")
               },
               new Product
               {
                   ProductID = 16,
                   ProductName = "Rice Basmati 5kg",
                   CategoryID = 5,
                   BrandID = 16, // Al Osra
                   SupplierID = 1,
                   UnitPrice = 88.50M, // Changed from 49.99 to 88.50
                   DiscountPercentage = 15.00m, // 15% discount
                   ReorderLevel = 0,
                   Barcode = "6291001001166",
                   ImageUrl = "https://cdn.mafrservices.com/sys-master-root/hea/h22/9780218855454/534136_main.jpg?im=Resize=480",
                   CreatedAt = DateTime.Parse("2025-06-16T01:58:17.2228308")
               },
               new Product
               {
                   ProductID = 17,
                   ProductName = "Chicken Nuggets 750g", // Changed from 1kg to 750g
                   CategoryID = 8,
                   BrandID = 18, // Sadia
                   SupplierID = 2,
                   UnitPrice = 30.50M, // Changed from 32.99 to 30.50
                   DiscountPercentage = 25.00m, // 25% discount
                   ReorderLevel = 0,
                   Barcode = "6291001001173",
                   ImageUrl = "https://cdn.mafrservices.com/pim-content/SAU/media/product/559102/1745844003/559102_main.jpg?im=Resize=480",
                   CreatedAt = DateTime.Parse("2025-06-16T01:58:17.2228311")
               },
               new Product
               {
                   ProductID = 18,
                   ProductName = "Mixed Vegetable 400g", // Changed from Mixed Vegetables to Mixed Vegetable
                   CategoryID = 8,
                   BrandID = 5, // Al Kabeer
                   SupplierID = 2,
                   UnitPrice = 9.25M, // Changed from 9.99 to 9.25
                   DiscountPercentage = 0.00m, // No discount
                   ReorderLevel = 0,
                   Barcode = "6291001001180",
                   ImageUrl = "https://cdn.mafrservices.com/sys-master-root/h94/h6d/52426818781214/52224_main.jpg?im=Resize=480",
                   CreatedAt = DateTime.Parse("2025-06-16T01:58:17.2228314")
               },
               new Product
               {
                   ProductID = 19,
                   ProductName = "Classic 2x Plies Tissues, 140 Tissues x5", // Changed from Facial Tissues 200pc
                   CategoryID = 11,
                   BrandID = 12, // Fine
                   SupplierID = 1,
                   UnitPrice = 20.75M, // Changed from 4.50 to 20.75
                   DiscountPercentage = 0.00m, // No discount
                   ReorderLevel = 0,
                   Barcode = "6291001001197",
                   ImageUrl = "https://cdn.mafrservices.com/pim-content/SAU/media/product/744488/1735138803/744488_main.jpg?im=Resize=480",
                   CreatedAt = DateTime.Parse("2025-06-16T01:58:17.2228317")
               },
               new Product
               {
                   ProductID = 20,
                   ProductName = "Hand Wash, Total 10, for 100 percent stronger germ protection in 10 seconds, 200ml",
                   CategoryID = 11,
                   SupplierID = 1,
                   BrandID = 11,
                   UnitPrice = 14.95M,
                   DiscountPercentage = 0.00m, // No discount
                   ReorderLevel = 0,
                   Barcode = "6291001001203",
                   ImageUrl = "https://m.media-amazon.com/images/I/61klMwXJPbL._AC_SL1500_.jpg",
                   CreatedAt = DateTime.Parse("2025-06-16T01:58:17.2228320")
               }
           );

            // Unique index for Sale InvoiceNumber
            modelBuilder.Entity<Sale>()
                .HasIndex(s => s.InvoiceNumber)
                .IsUnique();

            // Unique index for Invoice InvoiceNumber
            modelBuilder.Entity<Invoice>()
                .HasIndex(i => i.InvoiceNumber)
                .IsUnique();

            // Index for quick date-based queries on Sale
            modelBuilder.Entity<Sale>()
                .HasIndex(s => s.CreatedAt);

            // Consolidated InventoryTransaction configuration
            modelBuilder.Entity<InventoryTransaction>(entity =>
            {
                entity.HasOne(it => it.User)
                    .WithMany() // No navigation property on ApplicationUser side
                    .HasForeignKey(it => it.UserId)
                    .IsRequired()
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(it => it.ProductBatch)
                    .WithMany(pb => pb.InventoryTransactions)
                    .HasForeignKey(it => it.ProductBatchID)
                    .IsRequired()
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Property(it => it.TransactionType)
                    .IsRequired()
                    .HasMaxLength(20);

                entity.ToTable("InventoryTransactions", tb =>
                    tb.HasCheckConstraint("CK_InventoryTransaction_Quantity", "[Quantity] > 0"));
            });

            // Consolidated Invoice configuration
            modelBuilder.Entity<Invoice>(entity =>
            {
                entity.HasOne(i => i.Sale)
                    .WithOne(s => s.Invoice) // Ensure Sale has Invoice navigation property
                    .HasForeignKey<Invoice>(i => i.SaleID)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.Property(i => i.InvoiceNumber)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(i => i.SaleID)
                    .HasColumnName("SaleId"); // Optional: Explicitly name the column if needed
            });

            // Fix the Brand relationship for Product
            modelBuilder.Entity<Product>()
                .HasOne(p => p.Brand)
                .WithMany(b => b.Products)
                .HasForeignKey(p => p.BrandID)
                .OnDelete(DeleteBehavior.Restrict);

            // Expenses table configuration
            modelBuilder.Entity<Expense>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Amount).HasPrecision(18, 2);
                entity.Property(e => e.Date).IsRequired();
                entity.Property(e => e.Description).HasMaxLength(500);
            });

            // ProductBatch relationships
            modelBuilder.Entity<ProductBatch>(entity =>
            {
                entity.HasKey(pb => pb.BatchID);
                entity.Property(pb => pb.StockQuantity).IsRequired();
                entity.Property(pb => pb.ExpirationDate).IsRequired();
                entity.Property(pb => pb.CreatedAt).IsRequired();
                entity.HasOne(pb => pb.Product)
                    .WithMany(p => p.ProductBatches)
                    .HasForeignKey(pb => pb.ProductID)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            base.OnModelCreating(modelBuilder); // Call base only if extending a base context
        }

    }
}





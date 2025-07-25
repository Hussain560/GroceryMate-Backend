using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using GroceryMateApi.Models;

namespace GroceryMateApi.Data
{
    public static class DbInitializer
    {
        public static async Task Initialize(IServiceProvider serviceProvider)
        {
            var context = serviceProvider.GetRequiredService<GroceryStoreContext>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = serviceProvider.GetRequiredService<RoleManager<ApplicationRole>>();

            // Ensure database is created and migrated
            await context.Database.EnsureCreatedAsync();
            await context.Database.MigrateAsync();

            Console.WriteLine("Database created and migrated.");

            // Seed Roles
            string[] roleNames = { "Manager", "Employee" };
            foreach (var roleName in roleNames)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    var result = await roleManager.CreateAsync(new ApplicationRole(roleName));
                    if (!result.Succeeded)
                    {
                        throw new InvalidOperationException($"Failed to create role {roleName}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                    }
                }
            }

            // Seed Admin User
            var adminUser = await userManager.FindByNameAsync("admin@store.com");
            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = "admin@store.com",
                    Email = "admin@store.com",
                    FullName = "System Administrator",
                    EmailConfirmed = true,
                    CreatedAt = DateTime.UtcNow
                };
                var result = await userManager.CreateAsync(adminUser, "Admin123!");
                if (!result.Succeeded)
                {
                    throw new InvalidOperationException($"Failed to create admin user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                }
                
                result = await userManager.AddToRoleAsync(adminUser, "Manager");
                if (!result.Succeeded)
                {
                    throw new InvalidOperationException("Failed to add admin to Manager role");
                }
            }

            // Seed Employee User
            var employeeUser = await userManager.FindByEmailAsync("employee@store.com");
            if (employeeUser == null)
            {
                employeeUser = new ApplicationUser
                {
                    UserName = "employee@store.com",
                    Email = "employee@store.com",
                    FullName = "Store Employee",
                    EmailConfirmed = true,
                    CreatedAt = DateTime.UtcNow
                };
                var result = await userManager.CreateAsync(employeeUser, "Employee123!");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(employeeUser, "Employee");
                }
            }
            else if (!await userManager.IsInRoleAsync(employeeUser, "Employee"))
            {
                await userManager.AddToRoleAsync(employeeUser, "Employee");
            }

            // Seed Categories with transaction
            if (!await context.Categories.AnyAsync())
            {
                Console.WriteLine("Starting category seeding...");
                using var dbTransaction = await context.Database.BeginTransactionAsync();
                try
                {
                    var categories = new[]
                    {
                        new Category { CategoryName = "Fruits & Vegetables", Description = "Fresh produce" },
                        new Category { CategoryName = "Dairy", Description = "Milk, cheese, and eggs" },
                        new Category { CategoryName = "Beverages", Description = "Drinks and juices" },
                        new Category { CategoryName = "Bakery", Description = "Bread and pastries" }
                    };

                    await context.Categories.AddRangeAsync(categories);
                    await context.SaveChangesAsync();

                    // Verify all categories were added
                    var addedCategories = await context.Categories.ToListAsync();
                    var missingCategories = categories
                        .Select(c => c.CategoryName)
                        .Except(addedCategories.Select(c => c.CategoryName))
                        .ToList();

                    if (missingCategories.Any())
                    {
                        throw new InvalidOperationException(
                            $"Failed to add categories: {string.Join(", ", missingCategories)}");
                    }

                    await dbTransaction.CommitAsync();
                    Console.WriteLine("Categories added successfully");
                }
                catch
                {
                    await dbTransaction.RollbackAsync();
                    throw;
                }
            }

            // Verify Categories
            var categoryCount = await context.Categories.CountAsync();
            Console.WriteLine($"Category count: {categoryCount}");
            if (categoryCount != 4)
            {
                throw new InvalidOperationException($"Expected 4 categories, found {categoryCount}");
            }

            // Seed Suppliers
            if (!await context.Suppliers.AnyAsync())
            {
                Console.WriteLine("Seeding suppliers...");
                var suppliers = new[]
                {
                    new Supplier { SupplierName = "Fresh Foods Co", ContactName = "John Smith", Email = "john@freshfoods.com" },
                    new Supplier { SupplierName = "Dairy Express", ContactName = "Mary Johnson", Email = "mary@dairyexpress.com" }
                };
                
                foreach (var supplier in suppliers)
                {
                    await context.Suppliers.AddAsync(supplier);
                    await context.SaveChangesAsync();
                    Console.WriteLine($"Added supplier: {supplier.SupplierName}");
                }
            }

            // Verify Suppliers
            var supplierCount = await context.Suppliers.CountAsync();
            Console.WriteLine($"Supplier count: {supplierCount}");
            if (supplierCount != 2)
            {
                throw new InvalidOperationException($"Expected 2 suppliers, found {supplierCount}");
            }

            // Seed Products with Brand check
            if (!await context.Products.AnyAsync())
            {
                // Verify all required data exists before attempting to seed products
                var categoriesExist = await context.Categories.CountAsync() == 4;
                var suppliersExist = await context.Suppliers.CountAsync() == 2;

                if (!categoriesExist || !suppliersExist)
                {
                    throw new InvalidOperationException(
                        $"Missing required data. Categories: {(categoriesExist ? "OK" : "Missing")}, " +
                        $"Suppliers: {(suppliersExist ? "OK" : "Missing")}");
                }

                var categories = await context.Categories.ToDictionaryAsync(c => c.CategoryName, c => c.CategoryID);
                var suppliers = await context.Suppliers.ToDictionaryAsync(s => s.SupplierName, s => s.SupplierID);

                // Create a default brand if none exists
                var defaultBrand = await context.Brands.FirstOrDefaultAsync();
                if (defaultBrand == null)
                {
                    defaultBrand = new Brand { BrandName = "Generic Brand" };
                    context.Brands.Add(defaultBrand);
                    await context.SaveChangesAsync();
                }

                var products = new[]
                {
                    // Fruits & Vegetables
                    new Product { 
                        ProductName = "Fresh Apples",
                        CategoryID = categories["Fruits & Vegetables"],
                        SupplierID = suppliers["Fresh Foods Co"],
                        BrandID = defaultBrand.BrandID,
                        UnitPrice = 5.99m,
                        StockQuantity = 100,
                        ReorderLevel = 20,
                        Barcode = "1234567890123",
                        CreatedAt = DateTime.UtcNow
                    },
                    new Product { 
                        ProductName = "Bananas",
                        CategoryID = categories["Fruits & Vegetables"],
                        SupplierID = suppliers["Fresh Foods Co"],
                        BrandID = defaultBrand.BrandID,
                        UnitPrice = 2.99m,
                        StockQuantity = 150,
                        ReorderLevel = 30,
                        Barcode = "1234567890124",
                        CreatedAt = DateTime.UtcNow
                    },
                    new Product { 
                        ProductName = "Tomatoes",
                        CategoryID = categories["Fruits & Vegetables"],
                        SupplierID = suppliers["Fresh Foods Co"],
                        BrandID = defaultBrand.BrandID,
                        UnitPrice = 3.99m,
                        StockQuantity = 80,
                        ReorderLevel = 15,
                        Barcode = "1234567890125",
                        CreatedAt = DateTime.UtcNow
                    },
                    new Product { 
                        ProductName = "Carrots",
                        CategoryID = categories["Fruits & Vegetables"],
                        SupplierID = suppliers["Fresh Foods Co"],
                        BrandID = defaultBrand.BrandID,
                        UnitPrice = 2.49m,
                        StockQuantity = 120,
                        ReorderLevel = 25,
                        Barcode = "1234567890126",
                        CreatedAt = DateTime.UtcNow
                    },
                    
                    // Dairy Products
                    new Product { 
                        ProductName = "Organic Milk",
                        CategoryID = categories["Dairy"],
                        SupplierID = suppliers["Dairy Express"],
                        BrandID = defaultBrand.BrandID,
                        UnitPrice = 4.99m,
                        StockQuantity = 50,
                        ReorderLevel = 10,
                        Barcode = "2234567890123",
                        CreatedAt = DateTime.UtcNow
                    },
                    new Product { 
                        ProductName = "Cheddar Cheese",
                        CategoryID = categories["Dairy"],
                        SupplierID = suppliers["Dairy Express"],
                        BrandID = defaultBrand.BrandID,
                        UnitPrice = 6.99m,
                        StockQuantity = 40,
                        ReorderLevel = 8,
                        Barcode = "2234567890124",
                        CreatedAt = DateTime.UtcNow
                    },
                    new Product { 
                        ProductName = "Greek Yogurt",
                        CategoryID = categories["Dairy"],
                        SupplierID = suppliers["Dairy Express"],
                        BrandID = defaultBrand.BrandID,
                        UnitPrice = 3.99m,
                        StockQuantity = 60,
                        ReorderLevel = 12,
                        Barcode = "2234567890125",
                        CreatedAt = DateTime.UtcNow
                    },
                    new Product { 
                        ProductName = "Fresh Eggs",
                        CategoryID = categories["Dairy"],
                        SupplierID = suppliers["Dairy Express"],
                        BrandID = defaultBrand.BrandID,
                        UnitPrice = 5.49m,
                        StockQuantity = 45,
                        ReorderLevel = 10,
                        Barcode = "2234567890126",
                        CreatedAt = DateTime.UtcNow
                    },
                    
                    // Beverages
                    new Product { 
                        ProductName = "Orange Juice",
                        CategoryID = categories["Beverages"],
                        SupplierID = suppliers["Fresh Foods Co"],
                        BrandID = defaultBrand.BrandID,
                        UnitPrice = 4.49m,
                        StockQuantity = 70,
                        ReorderLevel = 15,
                        Barcode = "3234567890123",
                        CreatedAt = DateTime.UtcNow
                    },
                    new Product { 
                        ProductName = "Mineral Water",
                        CategoryID = categories["Beverages"],
                        SupplierID = suppliers["Fresh Foods Co"],
                        BrandID = defaultBrand.BrandID,
                        UnitPrice = 1.99m,
                        StockQuantity = 200,
                        ReorderLevel = 40,
                        Barcode = "3234567890124",
                        CreatedAt = DateTime.UtcNow
                    },
                    new Product { 
                        ProductName = "Apple Juice",
                        CategoryID = categories["Beverages"],
                        SupplierID = suppliers["Fresh Foods Co"],
                        BrandID = defaultBrand.BrandID,
                        UnitPrice = 3.99m,
                        StockQuantity = 60,
                        ReorderLevel = 12,
                        Barcode = "3234567890125",
                        CreatedAt = DateTime.UtcNow
                    },
                    
                    // Bakery
                    new Product { 
                        ProductName = "White Bread",
                        CategoryID = categories["Bakery"],
                        SupplierID = suppliers["Fresh Foods Co"],
                        BrandID = defaultBrand.BrandID,
                        UnitPrice = 2.99m,
                        StockQuantity = 30,
                        ReorderLevel = 8,
                        Barcode = "4234567890123",
                        CreatedAt = DateTime.UtcNow
                    },
                    new Product { 
                        ProductName = "Croissants",
                        CategoryID = categories["Bakery"],
                        SupplierID = suppliers["Fresh Foods Co"],
                        BrandID = defaultBrand.BrandID,
                        UnitPrice = 5.99m,
                        StockQuantity = 25,
                        ReorderLevel = 6,
                        Barcode = "4234567890124",
                        CreatedAt = DateTime.UtcNow
                    },
                    new Product { 
                        ProductName = "Whole Wheat Bread",
                        CategoryID = categories["Bakery"],
                        SupplierID = suppliers["Fresh Foods Co"],
                        BrandID = defaultBrand.BrandID,
                        UnitPrice = 3.99m,
                        StockQuantity = 35,
                        ReorderLevel = 8,
                        Barcode = "4234567890125",
                        CreatedAt = DateTime.UtcNow
                    }
                };
                
                await context.Products.AddRangeAsync(products);
                await context.SaveChangesAsync();
            }
            
            // Final verification
            var verification = new
            {
                Categories = await context.Categories.CountAsync(),
                Suppliers = await context.Suppliers.CountAsync(),
                Products = await context.Products.CountAsync()
            };
            
            Console.WriteLine($"Seeding complete. Categories: {verification.Categories}, " +
                            $"Suppliers: {verification.Suppliers}, Products: {verification.Products}");
        }
    }
}

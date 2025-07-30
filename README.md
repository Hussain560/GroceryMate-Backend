# GroceryMate Backend API

[![.NET Core](https://img.shields.io/badge/.NET%20Core-8.0-blue.svg)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/github/license/Hussain560/GroceryMate-Backend)](LICENSE)
[![Repo Size](https://img.shields.io/github/repo-size/Hussain560/GroceryMate-Backend)](https://github.com/Hussain560/GroceryMate-Backend)

## Table of Contents

- [Overview](#overview)
- [Architecture](#architecture)
- [File Structure](#file-structure)
- [Developer Guide](#developer-guide)
- [Documentation](#documentation)
- [Integration](#integration)
- [Contributing](#contributing)

## Overview

GroceryMate Backend API is built with ASP.NET Core and provides the core business logic, authentication, and data management for the GroceryMate application.

## Architecture

- **Subsystems:**  
  - **Sales:** Handles sales cart, transactions, invoices, and archiving.
  - **Inventory:** Manages products, stock levels, adjustments, and low stock reporting.
  - **Users:** Manages user registration, authentication, roles, and profiles.
  - **Reports:** Generates sales, inventory, and supplier reports.  
    - **Purpose:** Centralizes analytics and reporting for business insights.
    - **Planned Features:** Daily/weekly/monthly sales reports, inventory trends, supplier performance, export to PDF/CSV.
    - **Future Integration:** Real-time dashboards via SignalR, interactive charts using charting libraries (e.g., Chart.js), scheduled report delivery.
  - **Suppliers:** Manages supplier records and restock orders.

- **Authentication:**  
  Uses ASP.NET Core Identity with JWT tokens. Role-based access control for Manager and Employee.

- **Database:**  
  Entity Framework Core with SQL Server. Data models and relationships are defined in `Data/GroceryStoreContext.cs`.

## File Structure

```text
Backend/
└── GroceryMateApi/
    ├── Controllers/           # API endpoints for each subsystem
    │   ├── AuthController.cs
    │   ├── InventoryController.cs
    │   ├── SalesController.cs
    │   ├── UsersController.cs
    │   └── ... 
    ├── Models/                # Entity and DTO definitions
    │   ├── Product.cs
    │   ├── Category.cs
    │   ├── Supplier.cs
    │   ├── Sale.cs
    │   ├── SaleDetail.cs
    │   ├── ApplicationUser.cs
    │   ├── ApplicationRole.cs
    │   └── ...
    ├── Services/              # Business logic for subsystems
    │   ├── InventoryService.cs
    │   ├── SalesService.cs
    │   └── ...
    ├── Data/                  # EF Core context and database initializer
    │   ├── GroceryStoreContext.cs
    │   ├── DbInitializer.cs
    │   └── ...
    ├── docs/                  # Documentation, requirements, and diagrams
    │   ├── overview.md
    │   ├── usage.md
    │   ├── installation.md
    │   ├── changelog.md
    │   └── analysis/
    │       ├── requirements.md
    │       └── use_cases/
    │           ├── sales_use_cases.puml
    │           └── inventory_use_cases.puml
    ├── Program.cs             # API setup, middleware, and service registration
    ├── appsettings.json       # Configuration (connection strings, JWT, etc.)
    ├── README.md              # Project overview and structure
    └── .gitignore             # Ignore rules for source control
```

## Developer Guide

- **Build:**  
  Run `dotnet build` in the project root.

- **Run:**  
  Start the API with `dotnet run` (default port: 5125).

- **Test:**  
  Use [Swagger UI](http://localhost:5125/swagger) to test endpoints.

- **Database:**  
  Connection string is in `appsettings.json`. Use EF Core migrations for schema updates.

- **Authentication:**  
  Login via `/api/Auth/login` to receive a JWT token. Use the token for authenticated requests.

## Documentation

- Requirements and use case diagrams are in `docs/analysis/`.
- Subsystem requirements are structured in `requirements.md`.
- Use case diagrams are provided in PlantUML format.

## Integration

- Communicates with the frontend at [GroceryMate-Frontend](https://github.com/Hussain560/GroceryMate-Frontend).
- Complies with KSA VAT and data protection laws.

## Contributing

We welcome contributions! To get started:

1. Fork the repository and clone your fork.
2. Create a new branch for your feature or bugfix.
3. Follow existing code structure and naming conventions.
4. Add tests for new features where applicable.
5. Submit a pull request with a clear description of your changes.

For questions or suggestions, please open an issue.

---

For more details, see the documentation in the `docs/` folder.

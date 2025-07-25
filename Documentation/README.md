# GroceryMate API Documentation

## Authentication
Base URL: `http://localhost:5125/api`

### Login
```http
POST /api/auth/login
Content-Type: application/json

{
    "username": "admin@store.com",
    "password": "Admin123!"
}
```
Response:
```json
{
    "success": true,
    "token": "JWT_TOKEN",
    "user": {
        "id": 1,
        "username": "admin@store.com",
        "email": "admin@store.com",
        "fullName": "System Administrator",
        "roles": ["Manager"]
    }
}
```

## Products API

### Get All Products
```http
GET /api/product
Authorization: Bearer {token}
```

### Get Product by ID
```http
GET /api/product/{id}
Authorization: Bearer {token}
```

### Search Products
```http
GET /api/product/search?q={searchTerm}
Authorization: Bearer {token}
```

### Scan Barcode
```http
GET /api/product/barcode/{barcode}
Authorization: Bearer {token}
```

## Categories API

### Get All Categories
```http
GET /api/categories
Authorization: Bearer {token}
```

### Get Products by Category
```http
GET /api/categories/{id}/products
Authorization: Bearer {token}
```

## Sales API

### Create Sale
```http
POST /api/sales
Authorization: Bearer {token}
Content-Type: application/json

{
    "items": [
        {
            "productID": 1,
            "quantity": 2,
            "unitPrice": 1.99,
            "discountPercentage": 0,
            "subtotal": 3.98
        }
    ],
    "paymentMethod": "Cash",
    "cashReceived": 5.00,
    "change": 1.02,
    "subtotalBeforeDiscount": 3.98,
    "totalDiscountPercentage": 0,
    "totalDiscountAmount": 0,
    "subtotalAfterDiscount": 3.98,
    "totalVATAmount": 0.60,
    "VATPercentage": 15,
    "finalTotal": 4.58
}
```

### Get Sales History
```http
GET /api/sales
Authorization: Bearer {token}
```

### Get Sale Details
```http
GET /api/sales/{id}
Authorization: Bearer {token}
```

## Inventory API

### Get Inventory Status
```http
GET /api/inventory
Authorization: Bearer {token}
```

### Get Low Stock Items
```http
GET /api/inventory/lowstock
Authorization: Bearer {token}
```

### Record Restock
```http
POST /api/inventory/restock
Authorization: Bearer {token}
Content-Type: application/json

{
    "productId": 1,
    "quantity": 50,
    "notes": "Regular restock"
}
```

### Record Spoilage
```http
POST /api/inventory/spoilage
Authorization: Bearer {token}
Content-Type: application/json

{
    "productId": 1,
    "quantity": 2,
    "reason": "Expired products"
}
```

## Dashboard API

### Manager Dashboard
```http
GET /api/dashboard/manager
Authorization: Bearer {token}
```
Response:
```json
{
    "success": true,
    "data": {
        "totalProducts": 64,
        "lowStockCount": 5,
        "todaysSales": 1250.75,
        "recentTransactions": []
    }
}
```

### Employee Dashboard
```http
GET /api/dashboard/employee
Authorization: Bearer {token}
```

## User Management API

### Get All Users
```http
GET /api/user
Authorization: Bearer {token}
```

### Create User
```http
POST /api/user
Authorization: Bearer {token}
Content-Type: application/json

{
    "email": "employee@store.com",
    "password": "Employee123!",
    "fullName": "New Employee",
    "role": "Employee"
}
```

## Error Responses

All endpoints return errors in this format:
```json
{
    "success": false,
    "error": "Error message description"
}
```

## Authentication Notes

1. All endpoints except `/api/auth/login` require JWT token
2. Token format: `Bearer {token}`
3. Token validity: 7 days
4. Roles: "Manager", "Employee"

## Role-Based Access

### Manager Access:
- All endpoints

### Employee Access:
- Products (Read)
- Sales (Create, Read)
- Inventory (Read)
- Employee Dashboard

## Testing

Use Swagger UI: `http://localhost:5125/swagger`
1. Login via `/api/auth/login`
2. Click "Authorize" button
3. Enter `Bearer {token}`
4. Test endpoints

## Rate Limits
- None implemented

## Data Validation
- Required fields are enforced
- Proper error messages returned
- Stock quantities checked before sales

# Requirements

## Functional Requirements

### Sales Subsystem
- **FR-1.1**: Record a sale transaction including items, total, and payment method.
- **FR-1.2**: Update a sale transaction.
- **FR-1.3**: Search a sale by invoice number or date.
- **FR-1.4**: Print a sale invoice.
- **FR-1.5**: Archive a sale transaction.

### Inventory Subsystem
- **FR-2.1**: Record a product with details (name, brand, stock, barcode).
- **FR-2.2**: Update a product’s stock or details.
- **FR-2.3**: Search a product by name or barcode.
- **FR-2.4**: Print a low stock report.
- **FR-2.5**: Archive outdated product records.

## Non-Functional Requirements

### Security
- **NF-1.1**: Ensure all user data is encrypted in transit using HTTPS.
- **NF-1.2**: Implement role-based access control (RBAC) to restrict actions to Managers and Employees.
- **NF-1.3**: Protect against SQL injection and cross-site scripting (XSS) attacks.

### Performance
- **NF-2.1**: Ensure sales data is processed within 2 seconds.
- **NF-2.2**: Support 100 concurrent users without performance degradation.
- **NF-2.3**: Handle 50 inventory updates per minute.

### Usability
- **NF-3.1**: Ensure the UI loads within 1 second.
- **NF-3.2**: Provide a responsive design for mobile and desktop devices.
- **NF-3.3**: Display real-time stock updates in the UI.

### Reliability
- **NF-4.1**: Maintain 99.9% uptime for the API and front-end.
- **NF-4.2**: Ensure data consistency between stock updates and sales records.


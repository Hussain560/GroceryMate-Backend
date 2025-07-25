# Requirements

## Sales Subsystem

### Functional Requirements
- **FR-2.1**: Record a sale transaction including items, total, and payment method.
- **FR-2.2**: Update a sale transaction.
- **FR-2.3**: Search a sale by invoice number or date.
- **FR-2.4**: Print a sale invoice.
- **FR-2.5**: Archive a sale transaction.

### Non-Functional Requirements
- **NFR-2.1**: Ensure sales data is processed within 2 seconds.
- **NFR-2.2**: Support 100 concurrent users without performance degradation.

## Inventory Subsystem

### Functional Requirements
- **FR-3.1**: Record a product with details (name, brand, stock, barcode).
- **FR-3.2**: Update a product’s stock or details.
- **FR-3.3**: Search a product by name or barcode.
- **FR-3.4**: Print a low stock report.
- **FR-3.5**: Archive outdated product records.

### Non-Functional Requirements
- **NFR-3.1**: Maintain stock accuracy with real-time updates.
- **NFR-3.2**: Handle 50 inventory updates per minute.

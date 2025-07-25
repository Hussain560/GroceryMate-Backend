using GroceryMateApi.Data;
using Microsoft.EntityFrameworkCore;
using System;

namespace GroceryMateApi.Services
{
    public class InvoiceNumberGenerator
    {
        private readonly GroceryStoreContext _context;

        public InvoiceNumberGenerator(GroceryStoreContext context)
        {
            _context = context;
        }

        public async Task<string> GenerateInvoiceNumber()
        {
            string date = DateTime.UtcNow.ToString("yyyyMMdd");
            string prefix = $"INV{date}";
            
            // Get the last invoice number for today
            var lastInvoice = await _context.Sales
                .Where(s => s.InvoiceNumber.StartsWith(prefix))
                .OrderByDescending(s => s.InvoiceNumber)
                .Select(s => s.InvoiceNumber)
                .FirstOrDefaultAsync();

            int sequence = 1;
            if (lastInvoice != null)
            {
                // Extract the numeric suffix and increment
                string numericPart = lastInvoice.Substring(prefix.Length);
                if (int.TryParse(numericPart, out int lastSequence))
                {
                    sequence = lastSequence + 1;
                }
            }

            return $"{prefix}{sequence:D6}";
        }
    }
}

namespace GroceryMateApi.ViewModels
{
    public class SaleDetailViewModel
    {
        public int ProductID { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Subtotal { get; set; }
    }
}

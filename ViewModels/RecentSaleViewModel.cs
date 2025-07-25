namespace GroceryMateApi.ViewModels
{
    public class RecentSaleViewModel
    {
        public int SaleID { get; set; }
        public DateTime SaleDate { get; set; }
        public decimal FinalTotal { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public int ItemCount { get; set; }
    }
}

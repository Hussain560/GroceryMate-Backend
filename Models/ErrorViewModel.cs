namespace GroceryMateApi.Models
{
    public class ErrorViewModel
    {
        public string Message { get; set; } = string.Empty;
        public int StatusCode { get; set; }
        public string RequestId { get; set; } = string.Empty;
        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }
}

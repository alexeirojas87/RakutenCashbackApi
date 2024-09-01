namespace RakutenCashbackApi.Models
{
    public record StoreDto
    {
        public required string Name { get; set; }
        public double CashBackAmount { get; set; } = 0;
        public string ReferenceUri { get; set; } = string.Empty;
    }
}

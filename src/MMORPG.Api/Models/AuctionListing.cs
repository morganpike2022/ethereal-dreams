namespace MMORPG.Api.Models;

public class AuctionListing
{
    public Guid Id { get; set; }
    public Guid SellerId { get; set; }
    public int ItemId { get; set; }
    public int Quantity { get; set; } = 1;
    public long StartingBid { get; set; }
    public long? BuyoutPrice { get; set; }
    public long? CurrentBid { get; set; }
    public Guid? BidderId { get; set; }
    public string Status { get; set; } = "active";
    public short DurationHours { get; set; } = 48;
    public DateTimeOffset ListedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? SoldAt { get; set; }

    public Character Seller { get; set; } = null!;
    public Item Item { get; set; } = null!;
    public Character? Bidder { get; set; }
}

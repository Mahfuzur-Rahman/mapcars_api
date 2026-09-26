using Mapcars.Domain.Common;

namespace Mapcars.Domain.Entities;

/// <summary>
/// A customer's saved address (Home, Work, or a custom label). Many per customer.
/// </summary>
public class SavedPlace : BaseEntity
{
    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public required string Label { get; set; }
    public required string Address { get; set; }
    public double Lat { get; set; }
    public double Lng { get; set; }
}

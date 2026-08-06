using Mapcars.Domain.Common;

namespace Mapcars.Domain.Entities;

/// <summary>
/// A rider's saved address (Home, Work, or a custom label). Many per rider.
/// </summary>
public class SavedPlace : BaseEntity
{
    public Guid RiderId { get; set; }
    public Rider? Rider { get; set; }

    public required string Label { get; set; }
    public required string Address { get; set; }
    public double Lat { get; set; }
    public double Lng { get; set; }
}

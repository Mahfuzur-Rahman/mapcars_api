using Mapcars.Domain.Entities;

namespace Mapcars.Application.Payments.Interfaces;

/// <summary>A customer's saved cards (<c>customer_payment_methods</c>).</summary>
public interface ICustomerPaymentMethodRepository
{
    /// <summary>
    /// The card a new booking should be charged on: the customer's default if it
    /// is still usable, otherwise their most recently saved usable card. Null
    /// when they have none.
    /// </summary>
    Task<CustomerPaymentMethod?> GetChargeableAsync(Guid customerId, CancellationToken ct = default);
}

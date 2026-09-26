using Mapcars.Application.Contact.Dtos;

namespace Mapcars.Application.Contact.Interfaces;

public interface IContactService
{
    /// <summary>Forwards a contact-form enquiry to the support inbox.</summary>
    Task SubmitAsync(ContactRequest request, CancellationToken ct = default);
}

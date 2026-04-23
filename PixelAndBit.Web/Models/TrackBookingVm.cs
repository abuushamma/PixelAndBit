using System.ComponentModel.DataAnnotations;
using PixelAndBit.Domain.Entities;

namespace PixelAndBit.Web.Models;

public class TrackBookingVm
{
    [Display(Name = "Track.TicketReference")]
    [Required(ErrorMessage = "Validation.Required")]
    [RegularExpression("^PB-\\d{4}-[A-Z0-9]{4}$", ErrorMessage = "Validation.TicketFormat")]
    public string TicketReference { get; set; } = string.Empty;

    public Booking? Result { get; set; }
}


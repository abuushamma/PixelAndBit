using System.ComponentModel.DataAnnotations;

namespace PixelAndBit.Web.Models;

public class CreateBookingVm
{
    [Display(Name = "Booking.CustomerName")]
    [Required(ErrorMessage = "Validation.Required")]
    [MinLength(2, ErrorMessage = "Validation.MinLength")]
    [MaxLength(150, ErrorMessage = "Validation.MaxLength")]
    public string CustomerName { get; set; } = string.Empty;

    [Display(Name = "Booking.PhoneNumber")]
    [Required(ErrorMessage = "Validation.Required")]
    [RegularExpression("^07\\d{8}$", ErrorMessage = "Validation.PhoneJordan")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Display(Name = "Booking.DeviceType")]
    [Required(ErrorMessage = "Validation.Required")]
    public string DeviceType { get; set; } = "Phone";

    [Display(Name = "Booking.DeviceModel")]
    [Required(ErrorMessage = "Validation.Required")]
    [MinLength(2, ErrorMessage = "Validation.MinLength")]
    [MaxLength(200, ErrorMessage = "Validation.MaxLength")]
    public string DeviceModel { get; set; } = string.Empty;

    [Display(Name = "Booking.IssueDescription")]
    [Required(ErrorMessage = "Validation.Required")]
    [MinLength(20, ErrorMessage = "Validation.MinLength")]
    [MaxLength(2000, ErrorMessage = "Validation.MaxLength")]
    public string IssueDescription { get; set; } = string.Empty;

    [Range(0, 999999)]
    public decimal? EstimatedCost { get; set; }
}


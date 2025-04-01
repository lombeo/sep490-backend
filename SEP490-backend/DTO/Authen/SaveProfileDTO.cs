using System.ComponentModel.DataAnnotations;

namespace Sep490_Backend.DTO.Authen
{
	public class SaveProfileDTO : BaseRequest
	{
		public int UserId { get; set; }
		
		[Required(ErrorMessage = "Full name is required")]
		public string FullName { get; set; } = string.Empty;
		
		[Required(ErrorMessage = "Phone number is required")]
		[Phone(ErrorMessage = "Invalid phone number format")]
		public string PhoneNumber { get; set; } = string.Empty;
		
		public bool Gender { get; set; }
		
		[Required(ErrorMessage = "Address is required for construction site personnel")]
		public string Address { get; set; } = string.Empty;
		
		[Required(ErrorMessage = "Profile picture is required for identification")]
		public string PicProfile { get; set; } = string.Empty;
		
		public DateTime Dob { get; set; }
		public string? School { get; set; }
		public string? WorkAt { get; set; }
	}
}

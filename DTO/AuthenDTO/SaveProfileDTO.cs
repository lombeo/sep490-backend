namespace Api_Project_Prn.DTO.AuthenDTO
{
	public class SaveProfileDTO : BaseRequest
	{
		public int UserId { get; set; }
		public string FullName { get; set; }
		public string PhoneNumber { get; set; }
		public bool Gender { get; set; }
		public string Address { get; set; }
		public string PicProfile { get; set; }
		public DateTime Dob { get; set; }
		public string? School { get; set; }
		public string? WorkAt { get; set; }
	}
}

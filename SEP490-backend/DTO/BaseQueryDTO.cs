namespace Sep490_Backend.DTO
{
	public class BaseQuery : BaseRequest
	{
		public int PageIndex { get; set; } = 1;
		public int PageSize { get; set; } = 10;
		public int Skip => (PageIndex - 1) * PageSize;
		public int Total { get; set; }
	}

	public class BaseRequest
	{
		public int ActionBy { get; set; }
	}
}

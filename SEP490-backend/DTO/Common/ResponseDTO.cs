using Newtonsoft.Json;

namespace Sep490_Backend.DTO.Common
{
    public class ResponseDTO<T>
    {
        public ResponseDTO()
        {
            Errors = new List<ResponseError>();
        }
        [JsonProperty("code")]
        public int Code { get; set; } = (int)RESPONSE_CODE.OK;
        [JsonProperty("message")]
        public string Message { get; set; }
        [JsonProperty("errors")]
        public IList<ResponseError> Errors { get; set; }
        [JsonProperty("data")]
        public T Data { get; set; }
        [JsonProperty("meta")]
        public ResponseMeta Meta { get; set; }
        public void AddSingleError(string errorMsg)
        {
            Errors.Add(new ResponseError { Message = errorMsg });
        }
        public bool IsValid => (Errors?.Count ?? 0) == 0;
        public bool Success { get; set; } = false;

        public ResponseDTO(T data)
        {
            Data = data;
        }
        public ResponseDTO(int code, string message)
        {
            Code = code;
            Message = message;
        }

        public ResponseDTO(int code, string message, List<ResponseError> errors)
        {
            Code = code;
            Message = message;
            Errors = errors;
        }

        public bool Deleted { get; internal set; }
    }
    public class ResponseError
    {
        [JsonProperty("message")]
        public string Message { get; set; }
        [JsonProperty("field")]
        public string Field { get; set; }
    }
    public class ResponseMeta
    {
        [JsonProperty("total")]
        public int Total { get; set; }
        [JsonProperty("totalPage")]
        public int TotalPage => PageSize == 0 ? 0 : (Total % PageSize == 0 ? Total / PageSize : (Total / PageSize + 1));
        [JsonProperty("index")]
        public int Index { get; set; }
        [JsonProperty("pageSize")]
        public int PageSize { get; set; }
    }
    public enum RESPONSE_CODE
    {
        OK = 200,
        Created = 201,
        NoContent = 204,
        BadRequest = 400,
        Unauthorized = 401,
        Forbidden = 403,
        NotFound = 404,
        InternalServerError = 500
    }
}

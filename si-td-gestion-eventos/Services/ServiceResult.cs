namespace si_td_gestion_eventos.Services.Common
{
    public class ServiceResult<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
        public List<string> Errors { get; set; } = new();
        public string Message { get; set; } = string.Empty;

        public static ServiceResult<T> SuccessResult(T data, string message = "")
        {
            return new ServiceResult<T>
            {
                Success = true,
                Data = data,
                Message = message
            };
        }

        public static ServiceResult<T> FailureResult(List<string> errors)
        {
            return new ServiceResult<T>
            {
                Success = false,
                Errors = errors
            };
        }

        public static ServiceResult<T> FailureResult(string error)
        {
            return new ServiceResult<T>
            {
                Success = false,
                Errors = new List<string> { error }
            };
        }
    }
}
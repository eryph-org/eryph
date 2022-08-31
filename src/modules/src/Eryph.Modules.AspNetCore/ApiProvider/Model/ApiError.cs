using System;
using System.Text.Json.Serialization;

namespace Eryph.Modules.AspNetCore.ApiProvider.Model
{
    public class ApiError
    {
        public ApiError()
        {
            Error = new ApiErrorData {Code = "", Message = ""};
        }


        public ApiError(string code, string message) : this()
        {
            Error.Code = code;
            Error.Message = message;
        }

        [JsonPropertyName("error")] public ApiErrorData Error { get; set; }

        public static ApiError FromException(Exception exception, bool development)
        {
           var error = new ApiError("", "Internal error");

           if (!development)
               return error;

           error.Error = new ApiErrorData
           {
               Message = exception.Message,
           };

           return error;
        }
    }
}
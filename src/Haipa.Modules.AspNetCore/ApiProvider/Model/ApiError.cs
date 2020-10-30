using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.OData;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Haipa.Modules.AspNetCore.ApiProvider.Model
{
    public class ApiErrorBody
    {
        [Required]
        [JsonProperty("code")] public string Code { get; set; } = "";

        [Required]
        [JsonProperty("message")] public string Message { get; set; } = "";

        [JsonProperty("target")] public string Target { get; set; }

        [JsonExtensionData] public IDictionary<string, JToken> AdditionalData { get; set; }

        public ApiErrorBody()
        {
        }
    }

    public class ApiErrorData : ApiErrorBody
    {
        [JsonProperty("details")] public List<ApiErrorBody> Details { get; set; }
        [JsonProperty("innererror")] public InnerErrorData InnerError { get; set; }

        public class InnerErrorData
        {
            [JsonExtensionData] public IDictionary<string, JToken> AdditionalData { get; set; }

        }
    }

    public class ApiError
    {

        [JsonProperty("error")] public ApiErrorData Error { get; set; }


        public ApiError()
        {
            Error = new ApiErrorData{ Code = "", Message = ""};
        }


        public ApiError(string code, string message) : this()
        {
            Error.Code = code;
            Error.Message = message;
        }

        public ApiError(ODataError oDataError) : this()
        {
            Error = new ApiErrorData
            {
                Code = oDataError.ErrorCode ?? "",
                Message = oDataError.Message ?? "",
                Target = oDataError.Target,
                Details = oDataError.Details?.Select(
                    x => new ApiErrorBody
                    {
                        Code = x.ErrorCode ?? "",
                        Message = x.Message ?? "",
                        Target = x.Target
                    }).ToList()
            };

            if (oDataError.InnerError != null)
            {
                Error.InnerError = new ApiErrorData.InnerErrorData
                {
                    AdditionalData = new Dictionary<string, JToken>(
                        oDataError.InnerError.Properties.Select(x =>
                            new KeyValuePair<string, JToken>(x.Key, JToken.FromObject(SerializeODataValue(x.Value)))))
                };
                
            }
        }

        private static JToken SerializeODataValue(ODataValue oDataValue)
        {
            if(oDataValue is ODataPrimitiveValue pv)
                return JToken.FromObject(pv.Value);

            if (oDataValue is ODataCollectionValue cv)
                return JArray.FromObject(cv.Items);

            if(oDataValue is ODataUntypedValue ut)
                return JToken.Parse(ut.RawValue);

            throw new InvalidOperationException($"Serializing the odata type {oDataValue.TypeAnnotation.TypeName} is not supported for error data serialization");
        }


    }


}

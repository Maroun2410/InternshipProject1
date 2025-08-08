using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using InternshipProject1.Wrappers;

namespace InternshipProject1.Filters
{
    public class ApiResponseWrapperFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            // No action before the method executes
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
            if (context.Result is ObjectResult objectResult)
            {
                int statusCode = objectResult.StatusCode ?? 200;

                if (statusCode >= 200 && statusCode < 300 && objectResult.Value is not ApiResponse<object>)
                {
                    var wrapped = new ApiResponse<object>(
                        statusCode,
                        "Success",
                        objectResult.Value
                    );

                    context.Result = new ObjectResult(wrapped)
                    {
                        StatusCode = statusCode
                    };
                }
            }
        }
    }
}

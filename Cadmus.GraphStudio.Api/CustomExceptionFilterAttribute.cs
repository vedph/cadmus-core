using Microsoft.AspNetCore.Mvc.Filters;
using System.Net;
using System;
using Microsoft.AspNetCore.Mvc;

namespace Cadmus.GraphStudio.Api;

public sealed class CustomExceptionFilterAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        // recursively get the innermost exception
        Exception ex = context.Exception;
        while (ex.InnerException != null) ex = ex.InnerException;
        if (ex.InnerException != null) ex = ex.InnerException;

        // return ex message as JSON in error 500 result
        context.HttpContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
        context.HttpContext.Response.ContentType = "application/json";
        context.Result = new JsonResult(new
        {
            ex.Message,
            ex.StackTrace,
        });
    }
}
